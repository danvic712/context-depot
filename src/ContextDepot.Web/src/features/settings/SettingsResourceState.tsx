import { useTranslation } from "react-i18next";
import { RequestFeedback } from "@/components/feedback/RequestFeedback";
import { Skeleton } from "@/components/ui/skeleton";
import type { Resource } from "@/hooks/use-request-resource";

export function SettingsResourceState<T>({
  resource,
  onRetry,
}: {
  resource: Resource<T>;
  onRetry: () => void;
}) {
  const { t } = useTranslation();
  return (
    <>
      {resource.error && (
        <RequestFeedback
          title={t("settingsLoadError")}
          failure={resource.error}
          onRetry={onRetry}
          pending={resource.pending}
          stale={!!resource.data}
          compact
        />
      )}
      {!resource.data && resource.pending && (
        <div
          className="settings-loading"
          role="status"
          aria-label={t("loading")}
        >
          <Skeleton className="h-5 w-1/3" />
          <Skeleton className="h-12 w-full" />
          <Skeleton className="h-5 w-2/3" />
        </div>
      )}
    </>
  );
}
