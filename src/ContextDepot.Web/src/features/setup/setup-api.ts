import { httpRequest } from "@/lib/http-client";
import {
  parseInferenceProviders,
  parseIssuedKey,
  type IssuedKey,
} from "@/features/settings/settings-api";
import type { setupProviderRequests } from "./setup-inference-draft";
import type { CreateWorkspace } from "@/features/home/home-api";

export const setupSteps = [
  "workspace",
  "inference",
  "accessKey",
  "review",
] as const;
export type SetupStep = (typeof setupSteps)[number];
export interface SetupWorkspace {
  id: string;
  name: string;
  path: string;
  description: string | null;
}
export interface SetupStatus {
  state: "pending" | "inProgress" | "completed";
  workspace: SetupWorkspace | null;
  nextStep: SetupStep;
  mcpPath: "/mcp";
}

function object(value: unknown): Record<string, unknown> {
  if (!value || typeof value !== "object" || Array.isArray(value))
    throw new Error("Invalid setup response");
  return value as Record<string, unknown>;
}
function text(value: unknown): string {
  if (typeof value !== "string" || !value.trim())
    throw new Error("Invalid setup text");
  return value;
}
export function isSetupStep(value: unknown): value is SetupStep {
  return setupSteps.some((step) => step === value);
}
export function parseSetup(data: unknown): SetupStatus {
  const value = object(data);
  if (
    !["pending", "inProgress", "completed"].some(
      (state) => state === value.state,
    ) ||
    !isSetupStep(value.nextStep) ||
    value.mcpPath !== "/mcp"
  )
    throw new Error("Invalid setup state");
  let workspace: SetupWorkspace | null = null;
  if (value.workspace !== null) {
    const item = object(value.workspace);
    if (item.description !== null && typeof item.description !== "string")
      throw new Error("Invalid setup description");
    workspace = {
      id: text(item.id),
      name: text(item.name),
      path: text(item.path),
      description: item.description,
    };
  }
  if (
    (value.state === "pending" &&
      (workspace || value.nextStep !== "workspace")) ||
    (value.state === "inProgress" && !workspace) ||
    (value.state === "completed" && value.nextStep !== "review")
  )
    throw new Error("Inconsistent setup state");
  return {
    state: value.state as SetupStatus["state"],
    workspace,
    nextStep: value.nextStep,
    mcpPath: "/mcp",
  };
}
export function getSetup(signal?: AbortSignal) {
  return httpRequest({ url: "/setup", signal, parse: parseSetup });
}

export interface CompleteSetupDraft {
  workspace: CreateWorkspace;
  providers: ReturnType<typeof setupProviderRequests>;
  accessKeyName: string | null;
}
export interface SetupCompletion {
  status: SetupStatus;
  accessKey: IssuedKey | null;
}
export function parseSetupCompletion(data: unknown): SetupCompletion {
  const value = object(data);
  const status = parseSetup(value.status);
  const accessKey =
    value.accessKey === null ? null : parseIssuedKey(value.accessKey);
  if (
    status.state !== "completed" ||
    (accessKey &&
      (!status.workspace ||
        accessKey.key.workspaceIds.length !== 1 ||
        !accessKey.key.workspaceIds.includes(status.workspace.id)))
  )
    throw new Error("Invalid setup completion");
  return { status, accessKey };
}
export function getSetupInference(signal: AbortSignal) {
  return httpRequest({
    url: "/setup/inference",
    signal,
    parse: parseInferenceProviders,
  });
}
export function completeSetup(draft: CompleteSetupDraft, signal: AbortSignal) {
  return httpRequest({
    method: "POST",
    url: "/setup/complete",
    headers: { "X-ContextDepot-Management": "web" },
    data: draft,
    signal,
    timeout: 60_000,
    parse: parseSetupCompletion,
  });
}

export function currentSetupStep(
  status: SetupStatus,
  requested: string | null,
): SetupStep {
  if (!status.workspace) return "workspace";
  if (
    isSetupStep(requested) &&
    setupSteps.indexOf(requested) <= setupSteps.indexOf(status.nextStep)
  )
    return requested;
  return status.nextStep;
}
