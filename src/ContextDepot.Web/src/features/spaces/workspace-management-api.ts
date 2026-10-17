import { httpRequest } from "@/lib/http-client";
import { parseWorkspace } from "./workspace-summary";

export interface CreateWorkspace {
  name: string;
  path: string;
  description: string;
}

/** Creates a new workspace without updating an existing path. */
export function createWorkspace(data: CreateWorkspace, signal?: AbortSignal) {
  return httpRequest({
    method: "POST",
    url: "/workspaces",
    data,
    signal,
    parse: parseWorkspace,
  });
}
