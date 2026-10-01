import axios, { type AxiosRequestConfig } from "axios";
import { toast } from "sonner";

type FeedbackMessage = string | (() => string);

export interface HttpRequestOptions<T> extends AxiosRequestConfig {
  parse: (data: unknown) => T;
  beforeRequest?: () => Promise<void>;
  feedback?: {
    id?: string;
    success?: FeedbackMessage;
    error?: FeedbackMessage;
  };
}

export const httpClient = axios.create({
  baseURL: "/api",
  timeout: 10_000,
  headers: { Accept: "application/json" },
});

function resolveMessage(message: FeedbackMessage) {
  return typeof message === "function" ? message() : message;
}

export async function httpRequest<T>({
  parse,
  beforeRequest,
  feedback,
  ...config
}: HttpRequestOptions<T>): Promise<T> {
  try {
    await beforeRequest?.();
    const response = await httpClient.request<unknown>(config);
    const result = parse(response.data);
    if (feedback?.success)
      toast.success(
        resolveMessage(feedback.success),
        feedback.id ? { id: feedback.id } : undefined,
      );
    return result;
  } catch (error) {
    if (!isRequestCanceled(error) && feedback?.error)
      toast.error(
        resolveMessage(feedback.error),
        feedback.id ? { id: feedback.id } : undefined,
      );
    throw error;
  }
}

export function isRequestCanceled(error: unknown) {
  return axios.isCancel(error);
}
