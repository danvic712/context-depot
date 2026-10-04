import { useEffect, useMemo, useState } from "react";
import { isRequestCanceled } from "@/lib/http-client";
import { requestFailure, type RequestFailure } from "@/lib/request-failure";

export interface Resource<T> {
  data?: T;
  error?: RequestFailure;
  pending: boolean;
}
interface Response<T> extends Resource<T> {
  key: string;
  request: object;
}

export function useRequestResource<T>(
  key: string,
  attempt: number,
  load: ((signal: AbortSignal) => Promise<T>) | null,
  delay = 0,
): Resource<T> {
  const [response, setResponse] = useState<Response<T>>();
  // A new visit to the same key is a new request, even if its attempt is unchanged.
  const request = useMemo(
    () => ({ key, attempt, load, delay }),
    [key, attempt, load, delay],
  );
  useEffect(() => {
    const { key, load, delay } = request;
    if (!load) return;
    const controller = new AbortController();
    const run = () => {
      void Promise.resolve()
        .then(() => load(controller.signal))
        .then((data) => {
          if (!controller.signal.aborted)
            setResponse({ key, request, data, pending: false });
        })
        .catch((error) => {
          if (!controller.signal.aborted && !isRequestCanceled(error)) {
            const failure = requestFailure(error);
            setResponse((previous) => ({
              key,
              request,
              data:
                failure.retryable && previous?.key === key
                  ? previous.data
                  : undefined,
              error: failure,
              pending: false,
            }));
          }
        });
    };
    const timer = delay ? setTimeout(run, delay) : undefined;
    if (!delay) run();
    return () => {
      clearTimeout(timer);
      controller.abort();
    };
  }, [request]);
  const current = load && response?.key === key ? response : undefined;
  // Keep failures visible while retrying; a new query/selection never inherits old content.
  return {
    data: current?.data,
    error: current?.error,
    pending: Boolean(load && (current?.request !== request || current.pending)),
  };
}
