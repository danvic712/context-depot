import { httpRequest } from "@/lib/http-client";
import {
  parseWorkspace,
  parseKnowledge,
  type KnowledgeSummary,
  type WorkspaceSummary,
} from "@/features/home/home-api";

export interface Space extends WorkspaceSummary {
  subspaceCount: number;
}
export interface SpaceDirectory {
  asOf: string;
  items: Space[];
  totalCount: number;
  page: number;
  pageSize: number;
}
export interface SpaceDetail {
  workspace: Space;
  ancestors: { id: string; name: string; path: string }[];
}
export interface SpaceKnowledge {
  asOf: string;
  items: KnowledgeSummary[];
  totalCount: number;
  page: number;
  pageSize: number;
}

function record(value: unknown): Record<string, unknown> {
  if (!value || typeof value !== "object" || Array.isArray(value))
    throw new Error("Invalid workspace response");
  return value as Record<string, unknown>;
}
function text(value: unknown): string {
  if (typeof value !== "string") throw new Error("Invalid workspace response");
  return value;
}
function count(value: unknown, minimum = 0): number {
  if (
    typeof value !== "number" ||
    !Number.isSafeInteger(value) ||
    value < minimum
  )
    throw new Error("Invalid workspace count");
  return value;
}
function parseSpace(data: unknown): Space {
  return {
    ...parseWorkspace(data),
    subspaceCount: count(record(data).subspaceCount),
  };
}
export function parseDirectory(data: unknown): SpaceDirectory {
  const value = record(data);
  const asOf = text(value.asOf);
  if (!Array.isArray(value.items) || !Number.isFinite(Date.parse(asOf)))
    throw new Error("Invalid workspace directory");
  const pageSize = count(value.pageSize, 1);
  const totalCount = count(value.totalCount);
  if (
    pageSize > 60 ||
    value.items.length > pageSize ||
    value.items.length > totalCount
  )
    throw new Error("Invalid workspace directory");
  return {
    asOf,
    items: value.items.map(parseSpace),
    totalCount,
    page: count(value.page, 1),
    pageSize,
  };
}
export function parseSpaceDetail(data: unknown): SpaceDetail {
  const value = record(data);
  if (!Array.isArray(value.ancestors))
    throw new Error("Invalid workspace ancestors");
  return {
    workspace: parseSpace(value.workspace),
    ancestors: value.ancestors.map((ancestor) => {
      const item = record(ancestor);
      return {
        id: text(item.id),
        name: text(item.name),
        path: text(item.path),
      };
    }),
  };
}
export function getSpaceDirectory(
  parentId: string | undefined,
  page: number,
  signal: AbortSignal,
) {
  return httpRequest({
    method: "GET",
    url: "/workspaces/browse",
    params: { parentId, page, pageSize: 12 },
    signal,
    parse: parseDirectory,
  });
}
export function getSpace(id: string, signal: AbortSignal) {
  return httpRequest({
    method: "GET",
    url: `/workspaces/${encodeURIComponent(id)}`,
    signal,
    parse: parseSpaceDetail,
  });
}

export function getSpaceKnowledge(
  id: string,
  page: number,
  signal: AbortSignal,
) {
  return httpRequest({
    url: `/workspaces/${encodeURIComponent(id)}/knowledge`,
    params: { page, pageSize: 12 },
    signal,
    parse: (data): SpaceKnowledge => {
      const value = record(data);
      const asOf = text(value.asOf);
      const pageSize = count(value.pageSize, 1);
      const totalCount = count(value.totalCount);
      if (
        !Array.isArray(value.items) ||
        !Number.isFinite(Date.parse(asOf)) ||
        pageSize > 60 ||
        value.items.length > pageSize ||
        value.items.length > totalCount
      )
        throw new Error("Invalid workspace knowledge");
      return {
        asOf,
        items: value.items.map(parseKnowledge),
        totalCount,
        page: count(value.page, 1),
        pageSize,
      };
    },
  });
}
