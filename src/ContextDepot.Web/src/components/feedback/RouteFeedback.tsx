import { useTranslation } from "react-i18next";
import { SearchIcon } from "lucide-react";
import { Notice } from "@/components/content/PageElements";
import { Button } from "@/components/ui/button";
import { SidebarSkeleton } from "@/components/layout/Sidebar";
import { Skeleton } from "@/components/ui/skeleton";
import { HomeSkeleton } from "@/features/home/HomeSkeleton";
import "@/styles/header.css";

export function RouteLoading({ home = false }: { home?: boolean }) {
  const { t } = useTranslation();
  if (home)
    return (
      <div role="status" aria-label={t("loading")}>
        <HomeSkeleton />
      </div>
    );
  return (
    <Skeleton className="h-64 w-full" role="status" aria-label={t("loading")} />
  );
}

export function AppLoading({
  pathname = typeof window === "undefined" ? "/" : window.location.pathname,
}: {
  pathname?: string;
}) {
  const home = pathname === "/";
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
          className={`page${home ? " home" : ""}`}
          role="status"
          aria-label="Loading / 加载中"
        >
          {home ? (
            <HomeSkeleton />
          ) : (
            <div aria-hidden="true">
              <Skeleton className="mb-6 h-10 w-48" />
              <Skeleton className="h-64 w-full" />
            </div>
          )}
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

export function RouteError() {
  const { t } = useTranslation();
  return (
    <main className="page">
      <Notice
        icon={SearchIcon}
        title={t("pageLoadError")}
        detail={t("pageLoadErrorWhy")}
      >
        <Button onClick={() => window.location.reload()}>{t("reload")}</Button>
      </Notice>
    </main>
  );
}
