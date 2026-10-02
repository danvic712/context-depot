import { useTranslation } from "react-i18next";
import { RequestFeedback } from "@/components/feedback/RequestFeedback";
import { SettingsResourceSkeleton } from "./SettingsSkeleton";
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
        <div role="status" aria-label={t("loading")}>
          <SettingsResourceSkeleton />
        </div>
      )}
    </>
  );
}
