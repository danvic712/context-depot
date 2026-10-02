import { isRouteErrorResponse, useNavigate, useRouteError } from "react-router";
import { AppShell } from "@/components/layout/AppShell";
import { useAppearanceSettings } from "@/hooks/use-appearance-settings";
import { PageState } from "./PageState";
import { SpacesSkeleton } from "@/features/spaces/SpacesSkeleton";
import { SettingsSkeleton } from "@/features/settings/SettingsSkeleton";
import { useTranslation } from "react-i18next";
import { SidebarSkeleton } from "@/components/layout/Sidebar";
import { Skeleton } from "@/components/ui/skeleton";
import { HomeSkeleton } from "@/features/home/HomeSkeleton";
import { SearchPageSkeleton } from "@/features/knowledge/SearchSkeleton";
import { KnowledgeReaderSkeleton } from "@/features/knowledge/KnowledgeReaderSkeleton";
import "@/styles/header.css";

function RouteSkeleton({ pathname }: { pathname: string }) {
  if (/^\/(contexts|documents)\/[^/]+$/.test(pathname))
    return <KnowledgeReaderSkeleton />;
  if (pathname === "/") return <HomeSkeleton />;
  if (pathname === "/search") return <SearchPageSkeleton />;
  if (pathname === "/settings") return <SettingsSkeleton />;
  if (/^\/spaces(?:\/[^/]+)?$/.test(pathname))
    return <SpacesSkeleton detail={pathname !== "/spaces"} />;
  return (
    <div aria-hidden="true">
      <Skeleton className="h-16 w-2/3" />
      <Skeleton className="mt-6 h-48 w-full" />
    </div>
  );
}

export function RouteLoading({ pathname }: { pathname: string }) {
  const { t } = useTranslation();
  if (/^\/(contexts|documents)\/[^/]+$/.test(pathname))
    return <KnowledgeReaderSkeleton label={t("loading")} />;
  return (
    <div role="status" aria-label={t("loading")}>
      <RouteSkeleton pathname={pathname} />
    </div>
  );
}

export function AppLoading({
  pathname = typeof window === "undefined" ? "/" : window.location.pathname,
}: {
  pathname?: string;
}) {
  const home = pathname === "/";
  const search = pathname === "/search";
  return (
    <div className="shell startup-loading" aria-busy="true">
      <SidebarSkeleton />
      <div className={`frame${home ? " frame-home" : ""}`}>
        <div className="topbar" aria-hidden="true">
          <div className="brand-lockup">
            <Skeleton className="h-5 w-28" />
            <div className="loading-tagline">
              <Skeleton className="h-3 w-48" />
            </div>
          </div>
          <div className="top-controls">
            <Skeleton className="loading-preference" />
            <Skeleton className="loading-preference" />
          </div>
        </div>
        <main
          className={`page${home ? " home" : search ? " search" : ""}`}
          role="status"
          aria-label="Loading / 加载中"
        >
          <RouteSkeleton pathname={pathname} />
        </main>
        <footer aria-hidden="true">
          <Skeleton className="h-3 w-24" />
          <span>
            <Skeleton className="h-3 w-44" />
          </span>
        </footer>
      </div>
    </div>
  );
}

export function PageRouteError() {
  const error = useRouteError();
  const kind =
    isRouteErrorResponse(error) && error.status === 404
      ? "notFound"
      : isRouteErrorResponse(error) && error.status === 403
        ? "forbidden"
        : "error";
  return <PageState kind={kind} />;
}

export function RouteError() {
  const preferences = useAppearanceSettings();
  const navigate = useNavigate();
  return (
    <AppShell
      preferences={preferences}
      page="error"
      onSearch={() => {
        void navigate("/search");
      }}
    >
      <PageRouteError />
    </AppShell>
  );
}
