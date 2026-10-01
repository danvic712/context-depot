import {
  useCallback,
  useEffect,
  useMemo,
  useState,
  useRef,
  lazy,
  Suspense,
} from "react";
import {
  ScrollRestoration,
  Outlet,
  useMatches,
  useNavigation,
  parsePath,
  useLocation,
  useNavigate,
  useSearchParams,
  type NavigateOptions,
} from "react-router";
import { sampleKnowledge } from "./features/knowledge/sample-data";
import { useTranslation } from "react-i18next";
import type { Lang } from "@/lib/i18n";
import { useAppearanceSettings } from "./hooks/use-appearance-settings";
import { RequestFeedback } from "./components/feedback/RequestFeedback";
import { Sidebar } from "./components/layout/Sidebar";
import { Header } from "./components/layout/Header";
import { AppSelect } from "./components/ui/AppSelect";
import { Button } from "./components/ui/button";
import { RouteLoading } from "./components/feedback/RouteFeedback";
import type { AppContext, PageHandle } from "./hooks/use-app-context";
import type { PreviewState } from "./components/feedback/StatePreview";
import { normalizeKnowledgeParams } from "./features/knowledge/query-params";

const KnowledgeSearchDialog = lazy(
  () => import("./features/knowledge/KnowledgeSearchDialog"),
);

export default function App() {
  const [searchDialog, setSearchDialog] = useState<{
    query: string;
    session: number;
  } | null>(null);
  const searchOrigin = useRef<HTMLElement | null>(null);
  const openSearch = useCallback((query = "") => {
    searchOrigin.current =
      document.activeElement instanceof HTMLElement
        ? document.activeElement
        : null;
    setSearchDialog({ query, session: Date.now() });
  }, []);
  const { t, i18n } = useTranslation();
  const {
    theme,
    language: lang,
    languagePending,
    appearancePending,
    appearanceError,
    appearanceRefreshPending,
    refreshAppearance,
    onTheme: changeTheme,
    onLanguage: changeLang,
  } = useAppearanceSettings();
  const location = useLocation();
  const routerNavigate = useNavigate();
  const [rawParams, setParams] = useSearchParams();
  const params = useMemo(
    () => normalizeKnowledgeParams(rawParams),
    [rawParams],
  );
  const matched = useMatches().at(-1);
  const handle = (matched?.handle ?? {
    page: "home",
    title: "home",
    navigation: "home",
  }) as PageHandle;
  const page = handle.page;
  const itemId = matched?.params.knowledgeId ?? matched?.params.spaceId;
  const navigation = useNavigation();
  const homeLayout =
    navigation.state === "loading"
      ? navigation.location?.pathname === "/"
      : page === "home";
  useEffect(() => {
    if (params.toString() !== rawParams.toString()) {
      setParams(params, {
        replace: true,
        state: location.state,
        preventScrollReset: true,
      });
    }
  }, [params, rawParams, setParams, location.state]);
  const preview = params.get("preview") === "1";
  const previewState = (
    ["loading", "empty", "error", "permission", "degraded"].includes(
      params.get("state") ?? "",
    )
      ? params.get("state")
      : "success"
  ) as PreviewState;
  const activeLang: Lang = i18n.resolvedLanguage === "zh" ? "zh" : "en";
  const q = params.get("q") ?? "";
  const selectedKnowledge = sampleKnowledge.find(
    (item) =>
      item.id === itemId &&
      (page === "context" ? item.type === "context" : item.type === "document"),
  );
  const previewPath = useCallback(
    (path: string) => {
      const next = parsePath(path);
      const searchParams = new URLSearchParams(next.search);
      if (preview) searchParams.set("preview", "1");
      return { ...next, search: searchParams.size ? `?${searchParams}` : "" };
    },
    [preview],
  );
  const navigate = useCallback(
    (path: string, options?: NavigateOptions) => {
      void routerNavigate(previewPath(path), options);
    },
    [routerNavigate, previewPath],
  );
  function backFromDetail() {
    const state: unknown = location.state;
    if (
      state &&
      typeof state === "object" &&
      "from" in state &&
      typeof state.from === "string" &&
      state.from.startsWith("/") &&
      !state.from.startsWith("//")
    ) {
      void routerNavigate(-1);
    } else {
      navigate(
        selectedKnowledge
          ? `/spaces/${selectedKnowledge.workspaceId}`
          : "/spaces",
      );
    }
  }
  function setSearchFilter(key: "type" | "kind" | "workspace", value: string) {
    const next = new URLSearchParams();
    if (q) next.set("q", q);
    if (preview) next.set("preview", "1");
    for (const item of ["type", "kind", "workspace"] as const) {
      const current = item === key ? value : params.get(item);
      if (current && current !== "all") next.set(item, current);
    }
    if (key === "type" && value !== "contexts") next.delete("kind");
    if (key === "kind" && value !== "all") next.set("type", "contexts");
    setParams(next, { state: location.state, preventScrollReset: true });
  }
  function togglePreview() {
    const nextParams = new URLSearchParams(params);
    if (preview) {
      nextParams.delete("preview");
      nextParams.delete("state");
    } else nextParams.set("preview", "1");
    setParams(nextParams, {
      replace: true,
      state: location.state,
      preventScrollReset: true,
    });
  }
  function setPreviewState(value: PreviewState) {
    const nextParams = new URLSearchParams(params);
    if (value === "success") nextParams.delete("state");
    else nextParams.set("state", value);
    setParams(nextParams, {
      replace: true,
      state: location.state,
      preventScrollReset: true,
    });
  }
  function search(query: string) {
    const next = new URLSearchParams();
    if (query) next.set("q", query);
    if (page === "search")
      for (const key of ["type", "kind", "workspace"]) {
        const value = params.get(key);
        if (value) next.set(key, value);
      }
    navigate(`/search${next.size ? `?${next.toString()}` : ""}`);
  }
  useEffect(() => {
    document.documentElement.lang = activeLang === "zh" ? "zh-CN" : "en";
    document.title = `ContextDepot · ${t(handle.title)}`;
  }, [activeLang, handle.title, t]);
  useEffect(() => {
    const key = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === "k") {
        e.preventDefault();
        if (!document.querySelector("[role=dialog]")) openSearch();
      }
    };
    addEventListener("keydown", key);
    return () => removeEventListener("keydown", key);
  }, [openSearch]);
  useEffect(() => {
    if (
      page === "search" &&
      location.state?.focusSearch &&
      navigation.state === "idle"
    ) {
      document.getElementById("knowledge-search")?.focus();
    }
  }, [page, location.state, navigation.state]);
  const filteredSamples = sampleKnowledge.filter((item) => {
    if (
      q &&
      !`${item.title} ${item.summary} ${item.kind} ${item.workspace}`
        .toLowerCase()
        .includes(q.toLowerCase())
    )
      return false;
    if (params.get("type") === "contexts" && item.type !== "context")
      return false;
    if (params.get("type") === "documents" && item.type !== "document")
      return false;
    if (
      params.get("kind") &&
      params.get("kind") !== "all" &&
      item.kind.toLowerCase() !== params.get("kind")
    )
      return false;
    if (
      params.get("workspace") &&
      params.get("workspace") !== "all" &&
      item.workspaceId !== params.get("workspace")
    )
      return false;
    return true;
  });
  const context: AppContext = {
    preview,
    state: previewState,
    onRetry: () => setPreviewState("success"),
    query: q,
    params,
    results: filteredSamples,
    onSearch: page === "home" ? openSearch : search,
    onFilter: setSearchFilter,
    navigate,
    linkTo: previewPath,
    selectedId: itemId ?? "",
    view: params.get("view") ?? "all",
    item: selectedKnowledge,
    onBack: backFromDetail,
    theme,
    language: lang,
    languagePending,
    appearancePending,
    onTheme: changeTheme,
    onLanguage: changeLang,
  };
  return (
    <div className="shell shell-ready">
      {searchDialog && (
        <Suspense fallback={null}>
          <KnowledgeSearchDialog
            key={searchDialog.session}
            initialQuery={searchDialog.query}
            preview={preview}
            onClose={() => setSearchDialog(null)}
            onRestoreFocus={() => searchOrigin.current?.focus()}
          />
        </Suspense>
      )}
      <ScrollRestoration />
      <Sidebar linkTo={previewPath} onSearch={() => openSearch()} />
      <div className={`frame${homeLayout ? " frame-home" : ""}`}>
        <Header
          theme={theme}
          lang={lang}
          languagePending={languagePending}
          appearancePending={appearancePending}
          onTheme={changeTheme}
          onLanguage={changeLang}
          onSearch={() => openSearch()}
        />
        {appearanceError && (
          <RequestFeedback
            className="appearance-error"
            title={t("requestAppearanceError")}
            description={t("requestAppearanceWhy")}
            onRetry={refreshAppearance}
            pending={appearanceRefreshPending}
            compact
          />
        )}
        {(page !== "home" || preview) && (
          <div className={`preview-control ${preview ? "is-preview" : ""}`}>
            <span>{preview ? t("sampleHint") : ""}</span>
            {preview && handle.previewControls && (
              <div className="preview-state-select">
                <span>{t("previewState")}</span>
                <AppSelect
                  label={t("previewState")}
                  value={previewState}
                  onChange={(value) => setPreviewState(value as PreviewState)}
                  options={(
                    [
                      "success",
                      "loading",
                      "empty",
                      "error",
                      "permission",
                      "degraded",
                    ] as const
                  ).map((state) => ({ value: state, label: t(state) }))}
                />
              </div>
            )}
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={togglePreview}
            >
              {preview ? t("previewOff") : t("preview")}
            </Button>
          </div>
        )}
        <main
          className={`page ${homeLayout ? "home" : navigation.state === "loading" ? "loading" : page}`}
          aria-busy={navigation.state === "loading"}
        >
          {navigation.state === "loading" ? (
            <RouteLoading home={homeLayout} />
          ) : (
            <div className="route-content" key={location.pathname}>
              <Outlet key={itemId} context={context} />
            </div>
          )}
        </main>
        <footer>
          <span>ContextDepot</span>
          <span>{t("footerTagline")}</span>
        </footer>
      </div>
    </div>
  );
}
