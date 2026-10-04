import { useCallback, useState } from "react";
import { useSearchParams } from "react-router";
import { useRequestResource } from "@/hooks/use-request-resource";
import { getSpaceDirectory } from "./spaces-api";

export function useSpaceDirectory(parentId?: string, pageKey = "page") {
  const [params, setParams] = useSearchParams();
  const requestedPage = Number(params.get(pageKey) ?? 1);
  const page =
    Number.isSafeInteger(requestedPage) &&
    requestedPage > 0 &&
    requestedPage <= 100000
      ? requestedPage
      : 1;
  const [attempt, setAttempt] = useState(0);
  const load = useCallback(
    (signal: AbortSignal) => getSpaceDirectory(parentId, page, signal),
    [parentId, page],
  );
  const resource = useRequestResource(
    `${parentId ?? "root"}:${page}`,
    attempt,
    load,
  );
  function changePage(value: number) {
    const next = new URLSearchParams(params);
    if (value === 1) next.delete(pageKey);
    else next.set(pageKey, String(value));
    setParams(next, { preventScrollReset: true });
  }
  return {
    ...resource,
    page,
    changePage,
    refresh: () => setAttempt((value) => value + 1),
    onCreated: () => {
      changePage(1);
      setAttempt((value) => value + 1);
    },
  };
}
