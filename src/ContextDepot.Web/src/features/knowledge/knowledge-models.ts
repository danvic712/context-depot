export type KnowledgeType = "context" | "document";
export const contextKinds = [
  "fact",
  "preference",
  "decision",
  "goal",
  "state",
  "event",
  "observation",
] as const;
export type ContextKind = (typeof contextKinds)[number];

export interface KnowledgePreview {
  id: string;
  type: KnowledgeType;
  title: string;
  workspace: string;
  content: string;
  updatedAt: string;
}

export interface KnowledgeSummary {
  id: string;
  type: "context" | "document";
  kind: ContextKind | null;
  title: string;
  workspace: { id: string; path: string };
  updatedAt: string;
  indexStatus: "pending" | "indexed" | "failed" | null;
}

/** Resource identity is shared by navigation, selection, copying and search de-duplication. */
export const knowledgeKey = (item: Pick<KnowledgePreview, "type" | "id">) =>
  `${item.type}:${item.id}`;
