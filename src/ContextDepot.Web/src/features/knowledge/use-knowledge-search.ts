import { useCallback } from "react";
import { useRequestResource } from "@/hooks/use-request-resource";
import {
  getSearchWorkspaces,
  searchKnowledge,
  getKnowledgePreview,
  type SearchHit,
  type ContextKind,
} from "./search-api";

export function useKnowledgeSearch(
  query: string,
  workspace: string | undefined,
  retry: number,
  kind?: ContextKind,
  immediate = false,
) {
  const normalized = query.trim();
  const key = JSON.stringify([normalized, workspace, kind]);
  const load = useCallback(
    (signal: AbortSignal) =>
      searchKnowledge(normalized, workspace, signal, kind),
    [normalized, workspace, kind],
  );
  return useRequestResource(
    key,
    retry,
    normalized ? load : null,
    immediate ? 0 : 220,
  );
}

export function useSearchWorkspaces(retry: number) {
  return useRequestResource("search-workspaces", retry, getSearchWorkspaces);
}

export function useKnowledgePreview(
  item: SearchHit | undefined,
  retry: number,
) {
  const id = item?.id;
  const type = item?.type;
  const key = JSON.stringify([id, type]);
  const load = useCallback(
    (signal: AbortSignal) =>
      getKnowledgePreview({ id: id!, type: type! }, signal),
    [id, type],
  );
  return useRequestResource(key, retry, item ? load : null);
}
