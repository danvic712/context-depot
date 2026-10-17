import { knowledgeKey } from "./knowledge-models";
import {
  responseRecord as record,
  responseText as text,
} from "@/lib/response-values";
import type {
  KnowledgeType,
  ContextKind,
  KnowledgePreview,
} from "./knowledge-models";
import { httpRequest } from "@/lib/http-client";

export interface SearchHit {
  id: string;
  type: KnowledgeType;
  title: string;
  workspace: string;
  excerpt: string;
  kind: string;
}
export interface SearchResponse {
  items: SearchHit[];
  degraded: boolean;
  limit: number;
}
export interface SearchWorkspace {
  id: string;
  path: string;
}
function rows(value: unknown): unknown[] {
  if (!Array.isArray(value)) throw new Error("search.invalid_response");
  return value;
}
export function parseSearch(data: unknown): SearchResponse {
  const value = record(data);
  if (
    typeof value.degraded !== "boolean" ||
    !Number.isInteger(value.limit) ||
    (value.limit as number) < 1 ||
    (value.limit as number) > 50
  )
    throw new Error("search.invalid_response");
  const items = rows(value.items).map((row): SearchHit => {
    const item = record(row);
    if (item.type !== "context" && item.type !== "document")
      throw new Error("search.invalid_response");
    return {
      id: text(item.id),
      type: item.type,
      title: text(item.title),
      workspace: text(item.workspace),
      excerpt: text(item.excerpt),
      kind: text(item.kind),
    };
  });
  // A document can match several chunks. Keep one result per resource, preserving relevance order.
  const unique = new Map<string, SearchHit>();
  for (const item of items)
    if (!unique.has(knowledgeKey(item))) unique.set(knowledgeKey(item), item);
  return {
    items: [...unique.values()],
    degraded: value.degraded,
    limit: value.limit as number,
  };
}
export function parsePreview(data: unknown): KnowledgePreview {
  const item = record(data);
  if (item.type !== "context" && item.type !== "document")
    throw new Error("search.invalid_response");
  const updatedAt = text(item.updatedAt);
  if (!Number.isFinite(Date.parse(updatedAt)))
    throw new Error("search.invalid_response");
  return {
    id: text(item.id),
    type: item.type,
    title: text(item.title),
    workspace: text(item.workspace),
    content: text(item.content),
    updatedAt,
  };
}
export function searchKnowledge(
  query: string,
  workspace: string | undefined,
  signal?: AbortSignal,
  kind?: ContextKind,
) {
  return httpRequest({
    method: "GET",
    url: "/knowledge/search",
    params: { query, workspace, kind },
    signal,
    parse: parseSearch,
  });
}
export function getSearchWorkspaces(signal?: AbortSignal) {
  return httpRequest({
    method: "GET",
    url: "/knowledge/workspaces",
    signal,
    parse: (data) =>
      rows(data).map((row): SearchWorkspace => {
        const item = record(row);
        return { id: text(item.id), path: text(item.path) };
      }),
  });
}
export function getKnowledgePreview(
  item: Pick<SearchHit, "id" | "type">,
  signal?: AbortSignal,
  spaceId?: string,
) {
  return httpRequest({
    method: "GET",
    url: spaceId
      ? `/workspaces/${encodeURIComponent(spaceId)}/knowledge/${item.type}/${encodeURIComponent(item.id)}`
      : `/knowledge/${item.type}/${encodeURIComponent(item.id)}`,
    signal,
    parse: (data) => {
      const detail = parsePreview(data);
      if (
        detail.type !== item.type ||
        detail.id.toLowerCase() !== item.id.toLowerCase()
      )
        throw new Error("search.invalid_response");
      return detail;
    },
  });
}
