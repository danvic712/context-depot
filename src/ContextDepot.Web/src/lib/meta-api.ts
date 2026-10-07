import { httpRequest } from "./http-client";

export interface ApplicationMeta {
  version: string | null;
}

export function parseApplicationMeta(data: unknown): ApplicationMeta {
  if (!data || typeof data !== "object" || Array.isArray(data))
    throw new Error("Invalid application metadata");
  const value = data as Record<string, unknown>;
  const version = value.version;
  if (version !== null && (typeof version !== "string" || !version.trim()))
    throw new Error("Invalid application version");
  return { version };
}

export function getApplicationMeta(signal: AbortSignal) {
  return httpRequest({ url: "/meta", signal, parse: parseApplicationMeta });
}
