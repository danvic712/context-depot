import { useTranslation } from "react-i18next";
import { SearchIcon } from "lucide-react";
import { Notice } from "@/components/content/PageElements";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";

export function RouteLoading() {
  const { t } = useTranslation();
  return (
    <Skeleton className="h-64 w-full" role="status" aria-label={t("loading")} />
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
