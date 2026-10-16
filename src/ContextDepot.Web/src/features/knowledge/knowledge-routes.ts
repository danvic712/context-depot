import { type KnowledgeType } from "@/features/knowledge/knowledge-models";

export function knowledgePath(
  type: KnowledgeType,
  id: string,
  spaceId?: string,
) {
  const base = spaceId
    ? `/spaces/${encodeURIComponent(spaceId)}`
    : "/knowledge";
  return `${base}/${type === "context" ? "contexts" : "documents"}/${encodeURIComponent(id)}`;
}

export function spaceContentsPath(spaceId: string, archived = false) {
  return `/spaces/${encodeURIComponent(spaceId)}${archived ? "/archived" : ""}`;
}
