import { useTranslation } from "react-i18next";
import { SearchIcon } from "lucide-react";
import "@/styles/state-preview.css";
import { Notice } from "../content/PageElements";
import { RequestFeedback } from "./RequestFeedback";
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
      <RequestFeedback
        tone="info"
        title={t("degraded")}
        description={t("degradedWhy")}
        compact
      />
    );
  if (state === "error" || state === "permission")
    return (
      <RequestFeedback
        title={t(state)}
        failure={{
          kind: state === "permission" ? "forbidden" : "network",
          retryable: state === "error",
        }}
        onRetry={onRetry}
      />
    );
  const detail = t("emptyWhy");
  return (
    <div className="state-preview">
      <Notice icon={SearchIcon} title={t(state)} detail={detail} />
    </div>
  );
}
