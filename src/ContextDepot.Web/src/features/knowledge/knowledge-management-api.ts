import { httpRequest } from "@/lib/http-client";
import { type KnowledgePreview } from "@/features/knowledge/knowledge-models";

/** Deletes only the precise revision the user reviewed, using trusted Web management access. */
export function deleteKnowledge(
  item: Pick<KnowledgePreview, "id" | "type" | "updatedAt">,
  signal: AbortSignal,
) {
  return httpRequest({
    method: "DELETE",
    url: `/${item.type === "context" ? "contexts" : "documents"}/${encodeURIComponent(item.id)}`,
    params: { expectedUpdatedAt: item.updatedAt },
    headers: { "X-ContextDepot-Management": "web" },
    signal,
    parse: () => undefined,
  });
}
