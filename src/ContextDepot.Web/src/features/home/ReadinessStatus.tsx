import "@/styles/status.css";
import "@/styles/readiness-status.css";
import { useCallback, useEffect, useId, useRef, useState } from "react";
import { Popover } from "radix-ui";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { RefreshCwIcon, XIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { SubmitButton } from "@/components/content/SubmitButton";
import { usePopoverMotion } from "@/hooks/use-popover-motion";
import { getReadiness, type Readiness } from "./home-api";

const labels = {
  healthy: "homeReady",
  degraded: "homeDegraded",
  unhealthy: "homeUnavailable",
  unknown: "homeUnknown",
} as const;

const descriptions = {
  healthy: "homeReadinessHealthyDetail",
  degraded: "homeReadinessDegradedDetail",
  unhealthy: "homeReadinessUnhealthyDetail",
  unknown: "homeReadinessUnknownDetail",
} as const;

export function ReadinessStatus() {
  const { t } = useTranslation();
  const { open, present, onOpenChange, ref: popoverRef } = usePopoverMotion();
  const titleId = useId();
  const descriptionId = useId();
  const trigger = useRef<HTMLButtonElement>(null);
  const [status, setStatus] = useState<Readiness>("unknown");
  const [pending, setPending] = useState(true);
  const request = useRef<AbortController | null>(null);
  const fetchReadiness = useCallback(() => {
    request.current?.abort();
    const controller = new AbortController();
    request.current = controller;
    return getReadiness(controller.signal)
      .then((value) => {
        if (!controller.signal.aborted) setStatus(value);
      })
      .catch(() => {
        if (!controller.signal.aborted) setStatus("unknown");
      })
      .finally(() => {
        if (!controller.signal.aborted) setPending(false);
      });
  }, []);
  useEffect(() => {
    void fetchReadiness();
    const timer = setInterval(() => {
      if (!document.hidden) void fetchReadiness();
    }, 60_000);
    return () => {
      clearInterval(timer);
      request.current?.abort();
    };
  }, [fetchReadiness]);
  function refresh() {
    setPending(true);
    void fetchReadiness();
  }
  const tone =
    status === "healthy"
      ? "success"
      : status === "degraded"
        ? "warning"
        : status === "unhealthy"
          ? "danger"
          : "neutral";
  const label = t(
    pending && status === "unknown" ? "homeReadinessChecking" : labels[status],
  );
  return (
    <Popover.Root open={open} onOpenChange={onOpenChange}>
      <Popover.Trigger asChild>
        <button
          ref={trigger}
          type="button"
          className="rail-status"
          data-tone={tone}
          aria-label={`${t("homeReadinessHint")}: ${label}`}
        >
          <span className="status-dot" aria-hidden="true" />
          <span role="status">{label}</span>
        </button>
      </Popover.Trigger>
      {present && (
        <Popover.Portal forceMount>
          <Popover.Content
            ref={popoverRef}
            forceMount
            side="right"
            align="end"
            sideOffset={20}
            collisionPadding={12}
            className="readiness-popover"
            data-tone={tone}
            aria-labelledby={titleId}
            aria-describedby={descriptionId}
            aria-hidden={!open || undefined}
            inert={!open || undefined}
            onEscapeKeyDown={() =>
              trigger.current?.focus({ preventScroll: true })
            }
          >
            <div className="readiness-heading">
              <span className="status-dot" aria-hidden="true" />
              <h2 id={titleId}>{label}</h2>
              <Popover.Close asChild>
                <Button
                  variant="ghost"
                  size="icon-xs"
                  className="readiness-close"
                  aria-label={t("homeReadinessClose")}
                  onClick={() =>
                    trigger.current?.focus({ preventScroll: true })
                  }
                >
                  <XIcon aria-hidden="true" />
                </Button>
              </Popover.Close>
            </div>
            <p id={descriptionId} className="readiness-description">
              {t(
                pending && status === "unknown"
                  ? "homeReadinessCheckingDetail"
                  : descriptions[status],
              )}
            </p>
            <div className="readiness-actions">
              <SubmitButton
                type="button"
                variant="outline"
                size="sm"
                className="readiness-refresh"
                onClick={() => void refresh()}
                pending={pending}
                label={t("homeReadinessRefresh")}
                pendingLabel={t("homeReadinessChecking")}
                icon={RefreshCwIcon}
              />
              {(status === "degraded" || status === "unhealthy") && (
                <Button asChild size="sm" className="readiness-settings">
                  <Link to="/settings" onClick={() => onOpenChange(false)}>
                    {t("homeReadinessSettings")}
                  </Link>
                </Button>
              )}
            </div>
          </Popover.Content>
        </Popover.Portal>
      )}
    </Popover.Root>
  );
}
