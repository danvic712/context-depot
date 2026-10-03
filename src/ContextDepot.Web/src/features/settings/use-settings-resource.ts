import { useCallback, useEffect, useState } from "react";
import { useRequestResource } from "@/hooks/use-request-resource";

export function useSettingsResource<T>(
  load: (signal: AbortSignal) => Promise<T>,
  poll = false,
) {
  const [attempt, setAttempt] = useState(0);
  const refresh = useCallback(() => setAttempt((value) => value + 1), []);
  const resource = useRequestResource("settings", attempt, load);
  useEffect(() => {
    if (!poll) return;
    const onFocus = () => {
      if (document.visibilityState === "visible") refresh();
    };
    const timer = window.setInterval(onFocus, 30_000);
    window.addEventListener("focus", onFocus);
    return () => {
      clearInterval(timer);
      window.removeEventListener("focus", onFocus);
    };
  }, [poll, refresh]);
  return {
    ...resource,
    data:
      resource.error && !resource.error.retryable ? undefined : resource.data,
    refresh,
  };
}
