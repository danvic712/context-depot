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
  useLocation,
  useNavigate,
  useSearchParams,
} from "react-router";
import { useTranslation } from "react-i18next";
import type { Lang } from "@/lib/i18n";
import { useAppearanceSettings } from "./hooks/use-appearance-settings";
import { RequestFeedback } from "./components/feedback/RequestFeedback";
import { AppShell } from "./components/layout/AppShell";
import { RouteLoading } from "./components/feedback/RouteFeedback";
import type { AppContext, PageHandle } from "./hooks/use-app-context";
import { normalizeKnowledgeParams } from "./features/knowledge/query-params";
import { FeatureBoundary } from "./components/feedback/FeatureBoundary";
import { SearchDialogFailure } from "./features/knowledge/SearchDialogFailure";

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
  const navigate = useNavigate();
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
  const searchLayout =
    navigation.state === "loading"
      ? navigation.location?.pathname === "/search"
      : page === "search";
  useEffect(() => {
    if (params.toString() !== rawParams.toString()) {
      setParams(params, {
        replace: true,
        state: location.state,
        preventScrollReset: true,
      });
    }
  }, [params, rawParams, setParams, location.state]);
  const activeLang: Lang = i18n.resolvedLanguage === "zh" ? "zh" : "en";
  useEffect(() => {
    document.documentElement.lang = activeLang === "zh" ? "zh-CN" : "en";
    if (page !== "context" && page !== "document")
      document.title = `ContextDepot · ${t(handle.title)}`;
  }, [activeLang, handle.title, page, t]);
  useEffect(() => {
    const key = (e: KeyboardEvent) => {
      if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === "k") {
        e.preventDefault();
        if (!document.querySelector("[role=dialog]")) {
          if (page === "search")
            document.getElementById("knowledge-search")?.focus();
          else openSearch();
        }
      }
    };
    addEventListener("keydown", key);
    return () => removeEventListener("keydown", key);
  }, [openSearch, page]);
  useEffect(() => {
    if (
      page === "search" &&
      location.state?.focusSearch &&
      navigation.state === "idle"
    ) {
      document.getElementById("knowledge-search")?.focus();
    }
  }, [page, location.state, navigation.state]);
  const context: AppContext = {
    onSearch: openSearch,
    theme,
    language: lang,
    languagePending,
    appearancePending,
    onTheme: changeTheme,
    onLanguage: changeLang,
  };
  return (
    <>
      {searchDialog && (
        <FeatureBoundary
          key={searchDialog.session}
          fallback={
            <SearchDialogFailure
              onClose={() => setSearchDialog(null)}
              onRestoreFocus={() => searchOrigin.current?.focus()}
            />
          }
        >
          <Suspense fallback={null}>
            <KnowledgeSearchDialog
              key={searchDialog.session}
              initialQuery={searchDialog.query}
              onClose={() => setSearchDialog(null)}
              onRestoreFocus={() => searchOrigin.current?.focus()}
              onOpenPage={({ query, workspace, type }) => {
                const next = new URLSearchParams();
                if (query.trim()) next.set("q", query.trim());
                if (workspace) next.set("workspace", workspace);
                if (type !== "all")
                  next.set(
                    "type",
                    type === "context" ? "contexts" : "documents",
                  );
                setSearchDialog(null);
                void navigate(`/search${next.size ? `?${next}` : ""}`, {
                  state: { focusSearch: true },
                });
              }}
            />
          </Suspense>
        </FeatureBoundary>
      )}
      <ScrollRestoration />
      <AppShell
        preferences={context}
        home={homeLayout}
        page={
          homeLayout
            ? "home"
            : searchLayout
              ? "search"
              : navigation.state === "loading"
                ? "loading"
                : page
        }
        pending={navigation.state === "loading"}
        onSearch={() =>
          page === "search"
            ? document.getElementById("knowledge-search")?.focus()
            : openSearch()
        }
        feedback={
          appearanceError && (
            <RequestFeedback
              className="appearance-error"
              title={t("requestAppearanceError")}
              description={t("requestAppearanceWhy")}
              onRetry={refreshAppearance}
              pending={appearanceRefreshPending}
              compact
            />
          )
        }
      >
        {navigation.state === "loading" ? (
          <RouteLoading
            pathname={navigation.location?.pathname ?? location.pathname}
          />
        ) : (
          <div className="route-content" key={location.pathname}>
            <Outlet key={itemId} context={context} />
          </div>
        )}
      </AppShell>
    </>
  );
}
