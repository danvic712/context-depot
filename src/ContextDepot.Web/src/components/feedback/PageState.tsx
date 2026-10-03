import {
  ArrowRightIcon,
  FileQuestionIcon,
  HouseIcon,
  LockKeyholeIcon,
  RotateCwIcon,
  SearchIcon,
  ServerCrashIcon,
} from "lucide-react";
import { useEffect } from "react";
import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";
import "@/styles/page-state.css";

export function PageState({
  kind,
}: {
  kind: "error" | "notFound" | "forbidden";
}) {
  const { t } = useTranslation();
  const state = {
    error: {
      icon: ServerCrashIcon,
      code: "500",
      label: t("pageErrorLabel"),
      title: t("pageLoadError"),
      detail: t("pageLoadErrorWhy"),
      hint: t("pageErrorHint"),
    },
    notFound: {
      icon: FileQuestionIcon,
      code: "404",
      label: t("pageNotFoundLabel"),
      title: t("pageNotFound"),
      detail: t("pageNotFoundWhy"),
      hint: t("pageNotFoundHint"),
    },
    forbidden: {
      icon: LockKeyholeIcon,
      code: "403",
      label: t("pageForbiddenLabel"),
      title: t("pageForbidden"),
      detail: t("requestForbiddenWhy"),
      hint: t("pageForbiddenHint"),
    },
  }[kind];
  const Icon = state.icon;
  const fullscreen = kind !== "forbidden";
  useEffect(() => {
    const frame = requestAnimationFrame(() => {
      document.title = `${state.code} · ${state.title} · ContextDepot`;
    });
    return () => cancelAnimationFrame(frame);
  }, [state.code, state.title]);
  return (
    <section
      className="page-state"
      data-kind={kind}
      data-layout={fullscreen ? "fullscreen" : "contained"}
      aria-labelledby="page-state-title"
    >
      <div className="page-state-body">
        <div className="page-state-copy">
          <p className="page-state-label">
            <span className="page-state-dot" aria-hidden="true" />
            <span>{state.code}</span>
            <span className="page-state-label-divider" aria-hidden="true" />
            {state.label}
          </p>
          <h1 id="page-state-title">{state.title}</h1>
          <p className="page-state-description">{state.detail}</p>
          <div className="page-state-actions">
            {kind === "error" ? (
              <Button onClick={() => window.location.reload()}>
                <RotateCwIcon aria-hidden="true" />
                {t("reload")}
              </Button>
            ) : (
              <Button asChild>
                <Link to="/">
                  <HouseIcon aria-hidden="true" />
                  {t("pageReturnHome")}
                </Link>
              </Button>
            )}
            <Button asChild variant="outline">
              <Link to={kind === "error" ? "/" : "/search"}>
                {kind === "error" ? (
                  <HouseIcon aria-hidden="true" />
                ) : (
                  <SearchIcon aria-hidden="true" />
                )}
                {t(kind === "error" ? "pageReturnHome" : "pageSearchKnowledge")}
              </Link>
            </Button>
          </div>
        </div>
        <div className="page-state-art" aria-hidden="true">
          <span className="page-state-number">{state.code}</span>
          {fullscreen ? (
            <>
              <span className="page-state-orbit" />
              <span className="page-state-marker">
                <Icon strokeWidth={1.4} />
              </span>
            </>
          ) : (
            <>
              <div className="page-state-paper page-state-paper-back" />
              <div className="page-state-paper page-state-paper-front">
                <div className="page-state-paper-icon">
                  <Icon strokeWidth={1.4} />
                </div>
                <span className="page-state-paper-line" />
                <span className="page-state-paper-line" />
                <span className="page-state-paper-line" />
                <div className="page-state-paper-footer">
                  <span />
                  <span />
                  <span />
                </div>
              </div>
            </>
          )}
          <span className="page-state-art-cross page-state-art-cross-one">
            +
          </span>
          <span className="page-state-art-cross page-state-art-cross-two">
            +
          </span>
        </div>
      </div>
      <div className="page-state-footer">
        <p>{state.hint}</p>
        <Link to="/spaces">
          {t("pageBrowseSpaces")}
          <ArrowRightIcon aria-hidden="true" />
        </Link>
      </div>
    </section>
  );
}

export function PageNotFound() {
  return <PageState kind="notFound" />;
}

export function PageLoadError() {
  return <PageState kind="error" />;
}
