import { useTranslation } from "react-i18next";
import { SearchIcon } from "lucide-react";
import { Notice } from "@/components/content/PageElements";
import { Button } from "@/components/ui/button";
import { SidebarSkeleton } from "@/components/layout/Sidebar";
import { Skeleton } from "@/components/ui/skeleton";

export function RouteLoading() {
  const { t } = useTranslation();
  return (
    <Skeleton className="h-64 w-full" role="status" aria-label={t("loading")} />
  );
}

export function AppLoading() {
  return (
    <div className="shell" aria-busy="true">
      <SidebarSkeleton />
      <div className="frame">
        <div className="loading-header">
          <Skeleton className="h-8 w-32" />
        </div>
        <main className="page" role="status" aria-label="Loading / 加载中">
          <Skeleton className="mb-6 h-10 w-48" />
          <Skeleton className="h-64 w-full" />
        </main>
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
