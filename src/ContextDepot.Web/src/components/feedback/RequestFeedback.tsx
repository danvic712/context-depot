import {
  CircleAlertIcon,
  TriangleAlertIcon,
  InfoIcon,
  RotateCwIcon,
} from "lucide-react";
import { useTranslation } from "react-i18next";
import {
  Alert,
  AlertAction,
  AlertDescription,
  AlertTitle,
} from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { SubmitButton } from "@/components/content/SubmitButton";
import {
  requestFailureReasonKeys,
  type RequestFailure,
} from "@/lib/request-failure";
import { cn } from "@/lib/utils";
import "@/styles/request-feedback.css";

export function RequestFeedback({
  title,
  description,
  failure,
  onRetry,
  recoveryAction,
  headingLevel,
  pending = false,
  stale = false,
  tone = "error",
  compact = false,
  className,
}: {
  title: string;
  description?: string;
  failure?: RequestFailure;
  onRetry?: () => void | Promise<void>;
  recoveryAction?: { label: string; onClick: () => void };
  headingLevel?: 1 | 2 | 3;
  pending?: boolean;
  stale?: boolean;
  tone?: "error" | "info" | "warning";
  compact?: boolean;
  className?: string;
}) {
  const { t } = useTranslation();
  const Heading = headingLevel
    ? (`h${headingLevel}` as "h1" | "h2" | "h3")
    : null;
  const hasStaleContent = stale && failure?.retryable !== false;
  const feedbackTone = hasStaleContent && tone === "error" ? "warning" : tone;
  const canRetry = !!onRetry && failure?.retryable !== false;
  const detail = failure
    ? t(requestFailureReasonKeys[failure.kind])
    : (description ?? t("requestUnknownWhy"));
  const note = hasStaleContent
    ? t("requestStale")
    : failure
      ? description
      : undefined;
  const Icon =
    feedbackTone === "info"
      ? InfoIcon
      : feedbackTone === "warning"
        ? TriangleAlertIcon
        : CircleAlertIcon;
  return (
    <Alert
      variant={
        feedbackTone === "info"
          ? "soft-info"
          : feedbackTone === "warning"
            ? "soft-warning"
            : "soft-error"
      }
      role={feedbackTone === "error" ? "alert" : "status"}
      className={cn("request-feedback", className)}
      data-compact={compact || hasStaleContent || undefined}
      aria-busy={pending}
    >
      <Icon aria-hidden="true" />
      <div className="request-feedback-copy">
        <AlertTitle className="line-clamp-none">
          {Heading ? (
            <Heading tabIndex={-1}>
              {hasStaleContent ? t("requestRefreshError") : title}
            </Heading>
          ) : hasStaleContent ? (
            t("requestRefreshError")
          ) : (
            title
          )}
        </AlertTitle>
        <AlertDescription>
          <p>{detail}</p>
          {note && <p className="request-feedback-note">{note}</p>}
        </AlertDescription>
      </div>
      {(canRetry || recoveryAction) && (
        <AlertAction>
          {canRetry ? (
            <SubmitButton
              type="button"
              variant="outline"
              size="sm"
              pending={pending}
              label={t("retry")}
              pendingLabel={t("requestRetrying")}
              icon={RotateCwIcon}
              onClick={() => void onRetry?.()}
            />
          ) : (
            <Button
              type="button"
              variant="outline"
              size="sm"
              disabled={pending}
              onClick={recoveryAction?.onClick}
            >
              {recoveryAction?.label}
            </Button>
          )}
        </AlertAction>
      )}
    </Alert>
  );
}
