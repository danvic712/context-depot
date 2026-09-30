import { useTranslation } from "react-i18next";
import { SearchIcon } from "lucide-react";
import "@/styles/state-preview.css";
import { Notice } from "../content/PageElements";
import { Alert, AlertDescription, AlertTitle } from "../ui/alert";
import { Button } from "../ui/button";
import { Skeleton } from "../ui/skeleton";

export type PreviewState =
  "success" | "loading" | "empty" | "error" | "permission" | "degraded";
interface Props {
  state: PreviewState;
  onRetry: () => void;
}
export function StatePreview({ state, onRetry }: Props) {
  const { t } = useTranslation();
  if (state === "loading")
    return (
      <div className="skeleton-list" role="status" aria-label={t("loading")}>
        <Skeleton />
        <Skeleton />
        <Skeleton />
      </div>
    );
  if (state === "success") return null;
  if (state === "degraded")
    return (
      <Alert className="degraded-note" role="status">
        <AlertTitle>{t("degraded")}</AlertTitle>
        <AlertDescription>{t("degradedWhy")}</AlertDescription>
      </Alert>
    );
  const detail =
    state === "error"
      ? t("errorWhy")
      : state === "permission"
        ? t("permissionWhy")
        : t("emptyWhy");
  return (
    <div className="state-preview">
      <Notice icon={SearchIcon} title={t(state)} detail={detail} />
      {state === "error" && (
        <Button type="button" onClick={onRetry}>
          {t("retry")}
        </Button>
      )}
    </div>
  );
}
