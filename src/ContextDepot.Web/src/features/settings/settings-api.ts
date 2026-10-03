import { httpRequest } from "@/lib/http-client";

export interface SettingsOverview {
  depotName: string;
  databaseState: string;
  markdownState: string;
  semanticState: string;
  indexState: string;
  indexedCount: number | null;
  totalCount: number | null;
  mcpPath: string;
  version: string | null;
}
export interface AccessKey {
  id: string;
  name: string;
  prefix: string;
  createdAt: string;
  lastUsedAt: string | null;
  revokedAt: string | null;
  workspaceIds: string[];
}
export interface KeyWorkspace {
  id: string;
  name: string;
  path: string;
}
export interface AccessKeyList {
  items: AccessKey[];
  workspaces: KeyWorkspace[];
}
export interface IssuedKey {
  key: AccessKey;
  secret: string;
}
export interface AiRoute {
  providerId: string | null;
  capability: "embedding" | "chat";
  providerName: string | null;
  protocol: string;
  endpoint: string | null;
  model: string | null;
  dimensions: number | null;
  timeoutSeconds: number;
  hasApiKey: boolean;
  updatedAt: string;
  runtimeState: string;
  indexState: string;
  isApplied: boolean;
}
export interface AiProvider {
  id: string;
  name: string;
  protocol: string;
  endpoint: string | null;
  hasApiKey: boolean;
  updatedAt: string;
}
export interface AiProviderSettings {
  providers: AiProvider[];
  routes: AiRoute[];
}
export interface AiModelDraft {
  enabled: boolean;
  model: string;
  dimensions: number | null;
  timeoutSeconds: number;
}
export interface AiProviderDraft {
  id: string | null;
  name: string;
  endpoint: string;
  apiKey: string;
  updatedAt: string | null;
  embedding: AiModelDraft;
  chat: AiModelDraft;
  embeddingUpdatedAt: string;
  chatUpdatedAt: string;
}
export interface AiDraft {
  providerName: string;
  endpoint: string;
  model: string;
  dimensions: number | null;
  timeoutSeconds: number;
  apiKey: string;
  updatedAt: string;
}
function object(value: unknown): Record<string, unknown> {
  if (!value || typeof value !== "object" || Array.isArray(value))
    throw new Error("Invalid settings response");
  return value as Record<string, unknown>;
}
function string(value: unknown): string {
  if (typeof value !== "string") throw new Error("Invalid settings text");
  return value;
}
function nullable(value: unknown): string | null {
  return value === null ? null : string(value);
}
function number(value: unknown): number {
  if (typeof value !== "number" || !Number.isSafeInteger(value) || value < 0)
    throw new Error("Invalid settings number");
  return value;
}
function boolean(value: unknown): boolean {
  if (typeof value !== "boolean") throw new Error("Invalid settings flag");
  return value;
}
function array<T>(value: unknown, parse: (item: unknown) => T): T[] {
  if (!Array.isArray(value)) throw new Error("Invalid settings list");
  return value.map(parse);
}
function date(value: unknown): string {
  const result = string(value);
  if (!Number.isFinite(Date.parse(result)))
    throw new Error("Invalid settings date");
  return result;
}
function state(value: unknown, allowed: string[]): string {
  const result = string(value);
  if (!allowed.includes(result)) throw new Error("Invalid settings state");
  return result;
}
export function parseOverview(data: unknown): SettingsOverview {
  const v = object(data);
  if (v.mcpPath !== "/mcp") throw new Error("Invalid MCP path");
  return {
    depotName: string(v.depotName),
    databaseState: state(v.databaseState, ["available", "unavailable"]),
    markdownState: state(v.markdownState, ["available", "unavailable"]),
    semanticState: state(v.semanticState, [
      "configured",
      "unconfigured",
      "degraded",
    ]),
    indexState: state(v.indexState, [
      "complete",
      "repairing",
      "unknown",
      "unconfigured",
    ]),
    indexedCount: v.indexedCount === null ? null : number(v.indexedCount),
    totalCount: v.totalCount === null ? null : number(v.totalCount),
    mcpPath: string(v.mcpPath),
    version: nullable(v.version),
  };
}
export function parseAccessKey(data: unknown): AccessKey {
  const v = object(data);
  return {
    id: string(v.id),
    name: string(v.name),
    prefix: string(v.prefix),
    createdAt: date(v.createdAt),
    lastUsedAt: v.lastUsedAt === null ? null : date(v.lastUsedAt),
    revokedAt: v.revokedAt === null ? null : date(v.revokedAt),
    workspaceIds: array(v.workspaceIds, string),
  };
}
export function parseKeys(data: unknown): AccessKeyList {
  const v = object(data);
  return {
    items: array(v.items, parseAccessKey),
    workspaces: array(v.workspaces, (item) => {
      const w = object(item);
      return { id: string(w.id), name: string(w.name), path: string(w.path) };
    }),
  };
}
export function parseIssuedKey(data: unknown): IssuedKey {
  const v = object(data);
  const key = parseAccessKey(v.key),
    secret = string(v.secret);
  if (
    !secret.startsWith(`${key.prefix}.`) ||
    secret.length < key.prefix.length + 32
  )
    throw new Error("Invalid issued key");
  return { key, secret };
}
export function parseAiRoute(data: unknown): AiRoute {
  const v = object(data);
  if (v.capability !== "embedding" && v.capability !== "chat")
    throw new Error("Invalid AI capability");
  if (v.protocol !== "openai-compatible")
    throw new Error("Invalid AI protocol");
  return {
    providerId: v.providerId == null ? null : string(v.providerId),
    capability: v.capability,
    providerName: nullable(v.providerName),
    protocol: string(v.protocol),
    endpoint: nullable(v.endpoint),
    model: nullable(v.model),
    dimensions: v.dimensions === null ? null : number(v.dimensions),
    timeoutSeconds: number(v.timeoutSeconds),
    hasApiKey: boolean(v.hasApiKey),
    updatedAt: date(v.updatedAt),
    runtimeState: state(v.runtimeState, [
      "active",
      "pending",
      "unconfigured",
      "configured",
    ]),
    indexState: string(v.indexState),
    isApplied: boolean(v.isApplied),
  };
}
export function parseAiProviders(data: unknown): AiProviderSettings {
  const value = object(data);
  const providers = array(value.providers, (item) => {
    const provider = object(item);
    if (provider.protocol !== "openai-compatible")
      throw new Error("Invalid provider protocol");
    return {
      id: string(provider.id),
      name: string(provider.name),
      protocol: string(provider.protocol),
      endpoint: nullable(provider.endpoint),
      hasApiKey: boolean(provider.hasApiKey),
      updatedAt: date(provider.updatedAt),
    };
  });
  const routes = array(value.routes, parseAiRoute);
  if (
    routes.length !== 2 ||
    new Set(routes.map((route) => route.capability)).size !== 2 ||
    new Set(providers.map((provider) => provider.id)).size !==
      providers.length ||
    routes.some(
      (route) =>
        route.providerId &&
        !providers.some((provider) => provider.id === route.providerId),
    )
  )
    throw new Error("Invalid provider settings");
  return { providers, routes };
}
export function getAiProviders(signal: AbortSignal) {
  return httpRequest({
    url: "/settings/ai/providers",
    signal,
    parse: parseAiProviders,
  });
}
export function saveAiProvider(draft: AiProviderDraft, signal: AbortSignal) {
  const model = ({ enabled, ...value }: AiModelDraft) =>
    enabled ? value : null;
  return httpRequest({
    method: "PUT",
    headers: { "X-ContextDepot-Management": "web" },
    url: "/settings/ai/providers",
    signal,
    timeout: 60_000,
    data: {
      ...draft,
      embedding: model(draft.embedding),
      chat: model(draft.chat),
    },
    parse: parseAiProviders,
  });
}
export function validateAiProviderDraft(
  draft: AiProviderDraft,
  hasApiKey: boolean,
) {
  const errors = new Set<string>();
  for (const capability of ["embedding", "chat"] as const) {
    const model = draft[capability];
    const issues = validateAiDraft(
      {
        providerName: draft.name,
        endpoint: draft.endpoint,
        apiKey: draft.apiKey,
        updatedAt: draft.updatedAt ?? draft.embeddingUpdatedAt,
        model: model.enabled ? model.model : "unused",
        dimensions: model.enabled ? model.dimensions : 1,
        timeoutSeconds: model.enabled ? model.timeoutSeconds : 30,
      },
      capability,
      hasApiKey,
    );
    for (const issue of issues)
      errors.add(
        issue === "providerName"
          ? "name"
          : ["model", "dimensions", "timeoutSeconds"].includes(issue)
            ? `${capability}.${issue}`
            : issue,
      );
  }
  return [...errors];
}
export function getSettingsOverview(signal: AbortSignal) {
  return httpRequest({
    url: "/settings/overview",
    signal,
    parse: parseOverview,
  });
}
export function getAccessKeys(signal: AbortSignal) {
  return httpRequest({
    url: "/settings/access-keys",
    signal,
    parse: parseKeys,
  });
}
export function getAiSettings(signal: AbortSignal) {
  return httpRequest({
    url: "/settings/ai",
    signal,
    parse: (data) => {
      const routes = array(data, parseAiRoute);
      if (
        routes.length !== 2 ||
        new Set(routes.map((route) => route.capability)).size !== 2
      )
        throw new Error("Invalid AI routes");
      return routes;
    },
  });
}
export function createAccessKey(
  name: string,
  workspaceIds: string[],
  signal: AbortSignal,
) {
  return httpRequest({
    method: "POST",
    headers: { "X-ContextDepot-Management": "web" },
    url: "/settings/access-keys",
    data: { name, workspaceIds },
    signal,
    parse: parseIssuedKey,
  });
}
export function rotateAccessKey(id: string, signal: AbortSignal) {
  return httpRequest({
    method: "POST",
    headers: { "X-ContextDepot-Management": "web" },
    url: `/settings/access-keys/${encodeURIComponent(id)}/rotate`,
    signal,
    parse: parseIssuedKey,
  });
}
export function revokeAccessKey(id: string, signal: AbortSignal) {
  return httpRequest({
    method: "POST",
    headers: { "X-ContextDepot-Management": "web" },
    url: `/settings/access-keys/${encodeURIComponent(id)}/revoke`,
    signal,
    parse: parseAccessKey,
  });
}
export function updateAccessKeyGrants(
  id: string,
  workspaceIds: string[],
  signal: AbortSignal,
) {
  return httpRequest({
    method: "PUT",
    headers: { "X-ContextDepot-Management": "web" },
    url: `/settings/access-keys/${encodeURIComponent(id)}/grants`,
    data: { workspaceIds },
    signal,
    parse: parseAccessKey,
  });
}
export function saveAiRoute(
  capability: AiRoute["capability"],
  data: AiDraft,
  signal: AbortSignal,
) {
  return httpRequest({
    method: "PUT",
    headers: { "X-ContextDepot-Management": "web" },
    url: `/settings/ai/${capability}`,
    data,
    signal,
    timeout: 60_000,
    parse: parseAiRoute,
  });
}
export function validateAiDraft(
  draft: AiDraft,
  capability: AiRoute["capability"],
  hasApiKey: boolean,
) {
  const errors: string[] = [];
  if (!draft.providerName.trim() || draft.providerName.trim().length > 200)
    errors.push("providerName");
  if (!draft.model.trim() || draft.model.trim().length > 300)
    errors.push("model");
  try {
    const url = new URL(draft.endpoint);
    if (
      !["http:", "https:"].includes(url.protocol) ||
      url.username ||
      url.password ||
      url.search ||
      url.hash ||
      draft.endpoint.length > 2000
    )
      errors.push("endpoint");
  } catch {
    errors.push("endpoint");
  }
  if (
    capability === "embedding" &&
    (!Number.isInteger(draft.dimensions) ||
      !draft.dimensions ||
      draft.dimensions < 1 ||
      draft.dimensions > 16000)
  )
    errors.push("dimensions");
  if (
    !Number.isInteger(draft.timeoutSeconds) ||
    draft.timeoutSeconds < 1 ||
    draft.timeoutSeconds > 300
  )
    errors.push("timeoutSeconds");
  if ((!hasApiKey && !draft.apiKey.trim()) || draft.apiKey.length > 8192)
    errors.push("apiKey");
  return errors;
}
