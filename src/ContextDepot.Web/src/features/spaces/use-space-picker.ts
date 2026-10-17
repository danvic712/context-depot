import { useCallback, useState } from "react";
import { useRequestResource } from "@/hooks/use-request-resource";
import { getSpaceDirectory } from "./spaces-api";
import { defaultDirectoryFilters } from "./directory-query";
import { paginationDefaults } from "@/lib/pagination";

export function useSpacePicker(open: boolean) {
  const [query, setQuery] = useState("");
  const [page, setPage] = useState(1);
  const [attempt, setAttempt] = useState(0);
  const normalized = query.trim();
  const load = useCallback(
    (signal: AbortSignal) =>
      getSpaceDirectory(
        undefined,
        page,
        signal,
        { ...defaultDirectoryFilters, query: normalized },
        paginationDefaults.pageSize,
      ),
    [page, normalized],
  );
  const resource = useRequestResource(
    `${normalized}:${page}`,
    attempt,
    open ? load : null,
    normalized ? 160 : 0,
  );
  return {
    ...resource,
    query,
    page,
    changeQuery(value: string) {
      setQuery(value);
      setPage(1);
    },
    changePage: setPage,
    refresh: () => setAttempt((value) => value + 1),
  };
}
