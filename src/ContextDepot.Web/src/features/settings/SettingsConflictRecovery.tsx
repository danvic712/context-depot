import { useTranslation } from "react-i18next";
import { RotateCwIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { RequestFeedback } from "@/components/feedback/RequestFeedback";
import { type RequestFailure } from "@/lib/request-failure";

export function SettingsConflictRecovery({
  pending,
  error,
  onReload,
}: {
  pending: boolean;
  error?: RequestFailure;
  onReload: () => void;
}) {
  const { t } = useTranslation();
  return (
    <div className="flex flex-col gap-3">
      <Button
        type="button"
        variant="outline"
        disabled={pending || (error && !error.retryable)}
        onClick={onReload}
      >
        <RotateCwIcon
          aria-hidden="true"
          className={pending ? "animate-spin" : undefined}
        />
        {t(pending ? "loading" : "settingsReloadLatest")}
      </Button>
      {error && (
        <RequestFeedback
          title={t("settingsLoadError")}
          failure={error}
          compact
        />
      )}
    </div>
  );
}

export function SettingsRefreshButton({
  pending,
  onRefresh,
}: {
  pending: boolean;
  onRefresh: () => void;
}) {
  const { t } = useTranslation();
  return (
    <Button variant="outline" size="sm" disabled={pending} onClick={onRefresh}>
      <RotateCwIcon
        aria-hidden="true"
        className={pending ? "animate-spin" : undefined}
      />
      {t("settingsRefresh")}
    </Button>
  );
}
