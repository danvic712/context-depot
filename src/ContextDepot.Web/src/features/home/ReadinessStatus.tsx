import { useCallback, useEffect, useRef, useState } from "react";
import { Popover } from "radix-ui";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";
import { getReadiness, type Readiness } from "./home-api";

const labels = {
  healthy: "homeReady",
  degraded: "homeDegraded",
  unhealthy: "homeUnavailable",
  unknown: "homeUnknown",
} as const;

export function ReadinessStatus() {
  const { t } = useTranslation();
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
  return (
    <Popover.Root>
      <Popover.Trigger asChild>
        <button
          type="button"
          className={`rail-status readiness-${status}`}
          aria-label={`${t("homeReadinessHint")}: ${t(labels[status])}`}
        >
          <span className="status-dot" aria-hidden="true" />
          <span role="status">{t(labels[status])}</span>
        </button>
      </Popover.Trigger>
      <Popover.Portal>
        <Popover.Content
          side="right"
          align="end"
          sideOffset={16}
          className="z-50 max-w-72 rounded-lg border bg-popover p-5 text-sm shadow-lg"
        >
          <h2 className="font-semibold">{t(labels[status])}</h2>
          <p className="my-3 leading-relaxed text-muted-foreground">
            {t("homeReadinessDetail")}
          </p>
          <Button
            variant="outline"
            size="sm"
            onClick={() => void refresh()}
            disabled={pending}
          >
            {t(pending ? "loading" : "retry")}
          </Button>
        </Popover.Content>
      </Popover.Portal>
    </Popover.Root>
  );
}
