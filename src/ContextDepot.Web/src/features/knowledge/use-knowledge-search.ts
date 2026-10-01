import { useCallback } from "react";
import { useRequestResource } from "@/hooks/use-request-resource";
import { sampleKnowledge, sampleSpaces } from "./sample-data";
import {
  getSearchWorkspaces,
  searchKnowledge,
  getKnowledgePreview,
  type SearchResponse,
  type SearchHit,
  type KnowledgePreview,
  type SearchWorkspace,
} from "./search-api";

export function useKnowledgeSearch(
  query: string,
  workspace: string | undefined,
  preview: boolean,
  retry: number,
) {
  const normalized = query.trim();
  const key = JSON.stringify([normalized, workspace, preview]);
  const load = useCallback(
    (signal: AbortSignal): Promise<SearchResponse> =>
      preview
        ? Promise.resolve({
            items: sampleKnowledge
              .filter(
                (item) =>
                  (workspace === undefined || item.workspaceId === workspace) &&
                  `${item.title} ${item.summary} ${item.body}`
                    .toLowerCase()
                    .includes(normalized.toLowerCase()),
              )
              .map((item): SearchHit => ({
                id: item.id,
                type: item.type,
                title: item.title,
                workspace: item.workspaceId,
                excerpt: item.summary,
                kind: item.kind.toLowerCase(),
              })),
            degraded: false,
          })
        : searchKnowledge(normalized, workspace, signal),
    [normalized, workspace, preview],
  );
  return useRequestResource(key, retry, normalized ? load : null, 220);
}
export function useSearchWorkspaces(preview: boolean, retry: number) {
  const load = useCallback(
    (signal: AbortSignal): Promise<SearchWorkspace[]> =>
      preview
        ? Promise.resolve(
            sampleSpaces.map((item) => ({ id: item.id, path: item.path })),
          )
        : getSearchWorkspaces(signal),
    [preview],
  );
  return useRequestResource(JSON.stringify([preview]), retry, load);
}
export function useKnowledgePreview(
  item: SearchHit | undefined,
  preview: boolean,
  retry: number,
) {
  const id = item?.id;
  const type = item?.type;
  const key = JSON.stringify([id, type, preview]);
  const load = useCallback(
    (signal: AbortSignal): Promise<KnowledgePreview> => {
      if (!preview)
        return getKnowledgePreview({ id: id!, type: type! }, signal);
      const sample = sampleKnowledge.find(
        (sample) => sample.id === id && sample.type === type,
      )!;
      return Promise.resolve({
        id: sample.id,
        type: sample.type,
        title: sample.title,
        workspace: sample.workspaceId,
        content: sample.body,
        updatedAt: "2026-10-01T00:00:00Z",
      });
    },
    [id, type, preview],
  );
  return useRequestResource(key, retry, item ? load : null);
}
