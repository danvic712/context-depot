import { useCallback } from "react";
import { useKnowledgeReading } from "./knowledge-reading-context";
import { useRequestResource } from "@/hooks/use-request-resource";
import {
  getSearchWorkspaces,
  searchKnowledge,
  getKnowledgePreview,
  getRecentKnowledge,
  type SearchHit,
} from "./search-api";
import { type ContextKind } from "@/features/knowledge/knowledge-models";

export function useKnowledgeSearch(
  query: string,
  workspace: string | undefined,
  retry: number,
  kind?: ContextKind,
  immediate = false,
) {
  const revision = useKnowledgeReading()?.revision ?? 0;
  const normalized = query.trim();
  const key = JSON.stringify([normalized, workspace, kind]);
  const load = useCallback(
    (signal: AbortSignal) =>
      searchKnowledge(normalized, workspace, signal, kind),
    [normalized, workspace, kind],
  );
  return useRequestResource(
    key,
    retry + revision,
    normalized ? load : null,
    immediate ? 0 : 220,
  );
}

export function useSearchWorkspaces(retry: number) {
  return useRequestResource("search-workspaces", retry, getSearchWorkspaces);
}

export function useRecentKnowledge(
  enabled: boolean,
  workspace: string | undefined,
  type: string | undefined,
  kind: ContextKind | undefined,
  retry: number,
) {
  const revision = useKnowledgeReading()?.revision ?? 0;
  const load = useCallback(
    (signal: AbortSignal) => getRecentKnowledge(workspace, type, kind, signal),
    [workspace, type, kind],
  );
  return useRequestResource(
    JSON.stringify(["recent-knowledge", workspace, type, kind]),
    retry + revision,
    enabled ? load : null,
  );
}

export function useKnowledgePreview(
  item: SearchHit | undefined,
  retry: number,
) {
  const revision = useKnowledgeReading()?.revision ?? 0;
  const id = item?.id;
  const type = item?.type;
  const key = JSON.stringify([id, type]);
  const load = useCallback(
    (signal: AbortSignal) =>
      getKnowledgePreview({ id: id!, type: type! }, signal),
    [id, type],
  );
  return useRequestResource(key, retry + revision, item ? load : null);
}
