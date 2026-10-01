import {
  CircleAlertIcon,
  InfoIcon,
  LoaderCircleIcon,
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
import type { RequestFailure, RequestFailureKind } from "@/lib/request-failure";
import { cn } from "@/lib/utils";
import "@/styles/request-feedback.css";

const reasonKeys = {
  network: "requestNetworkWhy",
  timeout: "requestTimeoutWhy",
  forbidden: "requestForbiddenWhy",
  notFound: "requestNotFoundWhy",
  unavailable: "requestUnavailableWhy",
  invalidResponse: "requestInvalidResponseWhy",
  invalidQuery: "requestInvalidQueryWhy",
  unknown: "requestUnknownWhy",
} as const satisfies Record<RequestFailureKind, string>;

export function RequestFeedback({
  title,
  description,
  failure,
  onRetry,
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
  pending?: boolean;
  stale?: boolean;
  tone?: "error" | "info";
  compact?: boolean;
  className?: string;
}) {
  const { t } = useTranslation();
  const Icon = tone === "info" ? InfoIcon : CircleAlertIcon;
  return (
    <Alert
      variant={tone === "info" ? "soft-info" : "soft-error"}
      role={tone === "info" ? "status" : "alert"}
      className={cn("request-feedback", className)}
      data-compact={compact || undefined}
      aria-busy={pending}
    >
      <Icon aria-hidden="true" />
      <div className="request-feedback-copy">
        <AlertTitle className="line-clamp-none">{title}</AlertTitle>
        <AlertDescription>
          <p>
            {stale && <>{t("requestStale")} </>}
            {failure ? (
              <>
                {t(reasonKeys[failure.kind])} {description}
              </>
            ) : (
              (description ?? t("requestUnknownWhy"))
            )}
          </p>
        </AlertDescription>
      </div>
      {onRetry && failure?.retryable !== false && (
        <AlertAction>
          <Button
            variant="outline"
            size="sm"
            disabled={pending}
            onClick={() => void onRetry()}
          >
            {pending ? (
              <LoaderCircleIcon
                className="animate-spin"
                data-icon="inline-start"
                aria-hidden="true"
              />
            ) : (
              <RotateCwIcon data-icon="inline-start" aria-hidden="true" />
            )}
            {t(pending ? "requestRetrying" : "retry")}
          </Button>
        </AlertAction>
      )}
    </Alert>
  );
}
