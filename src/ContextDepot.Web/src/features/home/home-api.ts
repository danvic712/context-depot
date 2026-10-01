import { httpRequest } from "@/lib/http-client";

export interface WorkspaceSummary {
  id: string;
  name: string;
  description: string | null;
  path: string;
  contextCount: number;
  documentCount: number;
  activityAt: string;
}
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
export interface KnowledgeSummary {
  id: string;
  type: "context" | "document";
  kind: ContextKind | null;
  title: string;
  workspace: { id: string; path: string };
  updatedAt: string;
  indexStatus: "pending" | "indexed" | "failed" | null;
}
export interface ResourceCollection<T> {
  asOf: string;
  items: T[];
  hasMore: boolean;
}
export interface CreateWorkspace {
  name: string;
  path: string;
  description: string;
}

function object(value: unknown): Record<string, unknown> {
  if (!value || typeof value !== "object" || Array.isArray(value))
    throw new Error("Invalid resource response");
  return value as Record<string, unknown>;
}
function string(value: unknown): string {
  if (typeof value !== "string") throw new Error("Invalid resource response");
  return value;
}
function date(value: unknown): string {
  const result = string(value);
  if (!Number.isFinite(Date.parse(result)))
    throw new Error("Invalid resource timestamp");
  return result;
}
function count(value: unknown): number {
  if (typeof value !== "number" || !Number.isSafeInteger(value) || value < 0)
    throw new Error("Invalid resource count");
  return value;
}
function nullableString(value: unknown) {
  return value === null ? null : string(value);
}
export function parseWorkspace(data: unknown): WorkspaceSummary {
  const value = object(data);
  return {
    id: string(value.id),
    name: string(value.name),
    description: nullableString(value.description),
    path: string(value.path),
    contextCount: count(value.contextCount),
    documentCount: count(value.documentCount),
    activityAt: date(value.activityAt),
  };
}
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
function collection<T>(
  data: unknown,
  parse: (value: unknown) => T,
): ResourceCollection<T> {
  const value = object(data);
  if (!Array.isArray(value.items) || typeof value.hasMore !== "boolean")
    throw new Error("Invalid resource collection");
  return {
    asOf: date(value.asOf),
    items: value.items.map(parse),
    hasMore: value.hasMore,
  };
}
export function getWorkspaces(signal?: AbortSignal) {
  return httpRequest({
    method: "GET",
    url: "/workspaces",
    params: { sort: "-activityAt", limit: 3 },
    signal,
    parse: (data) => collection(data, parseWorkspace),
  });
}
export function getKnowledge(signal?: AbortSignal) {
  return httpRequest({
    method: "GET",
    url: "/knowledge",
    params: { sort: "-updatedAt", limit: 3 },
    signal,
    parse: (data) => collection(data, parseKnowledge),
  });
}
export function createWorkspace(data: CreateWorkspace, signal?: AbortSignal) {
  return httpRequest({
    method: "POST",
    url: "/workspaces",
    data,
    signal,
    parse: parseWorkspace,
  });
}

export type Readiness = "healthy" | "degraded" | "unhealthy" | "unknown";
export function parseReadiness(data: unknown): Readiness {
  const value = object(data);
  if (
    value.status === "healthy" ||
    value.status === "degraded" ||
    value.status === "unhealthy"
  )
    return value.status;
  throw new Error("Invalid readiness response");
}
export function getReadiness(signal?: AbortSignal) {
  // Unhealthy readiness is a valid JSON response, even with HTTP 503.
  return httpRequest({
    method: "GET",
    url: "/readyz",
    baseURL: "/",
    signal,
    validateStatus: (status) => status === 200 || status === 503,
    parse: parseReadiness,
  });
}
