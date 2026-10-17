import {
  responseRecord as object,
  responseText as string,
  responseDate as date,
} from "@/lib/response-values";
import {
  contextKinds,
  type ContextKind,
  type KnowledgeSummary,
} from "./knowledge-models";

export function parseKnowledge(data: unknown): KnowledgeSummary {
  const value = object(data);
  const workspace = object(value.workspace);
  const type = value.type;
  const kind = value.kind;
  const status = value.indexStatus;
  if (type !== "context" && type !== "document")
    throw new Error("Invalid knowledge type");
  if (
    type === "context" &&
    (!contextKinds.includes(kind as ContextKind) || status !== null)
  )
    throw new Error("Invalid context response");
  if (
    type === "document" &&
    (kind !== null ||
      !["pending", "indexed", "failed"].includes(string(status)))
  )
    throw new Error("Invalid document response");
  return {
    id: string(value.id),
    type,
    kind: kind as KnowledgeSummary["kind"],
    title: string(value.title),
    workspace: { id: string(workspace.id), path: string(workspace.path) },
    updatedAt: date(value.updatedAt),
    indexStatus: status as KnowledgeSummary["indexStatus"],
  };
}
