import { useEffect, useState } from "react";
import { isRequestCanceled } from "@/lib/http-client";
import { requestFailure, type RequestFailure } from "@/lib/request-failure";

export interface Resource<T> {
  data?: T;
  error?: RequestFailure;
  pending: boolean;
}
interface Response<T> extends Resource<T> {
  key: string;
  attempt: number;
}

export function useRequestResource<T>(
  key: string,
  attempt: number,
  load: ((signal: AbortSignal) => Promise<T>) | null,
  delay = 0,
): Resource<T> {
  const [response, setResponse] = useState<Response<T>>();
  useEffect(() => {
    if (!load) return;
    const controller = new AbortController();
    const run = () => {
      void load(controller.signal)
        .then((data) => {
          if (!controller.signal.aborted)
            setResponse({ key, attempt, data, pending: false });
        })
        .catch((error) => {
          if (!controller.signal.aborted && !isRequestCanceled(error))
            setResponse((previous) => ({
              key,
              attempt,
              data: previous?.key === key ? previous.data : undefined,
              error: requestFailure(error),
              pending: false,
            }));
        });
    };
    const timer = delay ? setTimeout(run, delay) : undefined;
    if (!delay) run();
    return () => {
      clearTimeout(timer);
      controller.abort();
    };
  }, [key, attempt, load, delay]);
  const current = response?.key === key ? response : undefined;
  // Keep failures visible while retrying; a new query/selection never inherits old content.
  return {
    data: current?.data,
    error: current?.error,
    pending: Boolean(load && current?.attempt !== attempt),
  };
}
