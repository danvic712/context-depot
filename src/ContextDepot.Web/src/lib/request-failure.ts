import { isAxiosError } from "axios";

export type RequestFailureKind =
  | "network"
  | "timeout"
  | "forbidden"
  | "notFound"
  | "unavailable"
  | "invalidResponse"
  | "invalidQuery"
  | "unknown";
export interface RequestFailure {
  kind: RequestFailureKind;
  retryable: boolean;
}

export const requestFailureReasonKeys = {
  network: "requestNetworkWhy",
  timeout: "requestTimeoutWhy",
  forbidden: "requestForbiddenWhy",
  notFound: "requestNotFoundWhy",
  unavailable: "requestUnavailableWhy",
  invalidResponse: "requestInvalidResponseWhy",
  invalidQuery: "requestInvalidQueryWhy",
  unknown: "requestUnknownWhy",
} as const satisfies Record<RequestFailureKind, string>;

// Never display transport exceptions or unlocalized server text in the UI.
export function requestFailure(error: unknown): RequestFailure {
  if (!isAxiosError(error))
    return {
      kind: error instanceof Error ? "invalidResponse" : "unknown",
      retryable: true,
    };
  const status = error.response?.status;
  if (status === 401 || status === 403)
    return { kind: "forbidden", retryable: false };
  if (status === 404) return { kind: "notFound", retryable: false };
  if (status === 400 || status === 422)
    return { kind: "invalidQuery", retryable: false };
  if (error.code === "ECONNABORTED" || error.code === "ETIMEDOUT")
    return { kind: "timeout", retryable: true };
  if (!error.response) return { kind: "network", retryable: true };
  return { kind: "unavailable", retryable: true };
}
