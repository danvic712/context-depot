import {
  responseRecord as object,
  responseDate as date,
} from "@/lib/response-values";
import { parseWorkspace } from "@/features/spaces/workspace-summary";
import { parseKnowledge } from "@/features/knowledge/knowledge-summary";
import { httpRequest } from "@/lib/http-client";

export interface ResourceCollection<T> {
  asOf: string;
  items: T[];
  hasMore: boolean;
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
