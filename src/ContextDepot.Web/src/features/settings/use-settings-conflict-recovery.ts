import { useEffect, useRef, useState } from "react";
import { requestFailure, type RequestFailure } from "@/lib/request-failure";

export function useSettingsConflictRecovery<T>(
  load: (signal: AbortSignal) => Promise<T>,
) {
  const [pending, setPending] = useState(false);
  const [error, setError] = useState<RequestFailure>();
  const request = useRef<AbortController | null>(null);
  useEffect(() => () => request.current?.abort(), []);
  async function reload(restore: (latest: T) => void) {
    if (request.current && !request.current.signal.aborted) return;
    const controller = new AbortController();
    request.current = controller;
    setPending(true);
    setError(undefined);
    try {
      const latest = await load(controller.signal);
      if (!controller.signal.aborted) restore(latest);
    } catch (failure) {
      if (!controller.signal.aborted) setError(requestFailure(failure));
    } finally {
      if (!controller.signal.aborted) {
        request.current = null;
        setPending(false);
      }
    }
  }
  return { pending, error, reload };
}
