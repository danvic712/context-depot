import { redirect, type LoaderFunctionArgs } from "react-router";
import { getSetup, type SetupStatus } from "./setup-api";
import { requestFailure, type RequestFailure } from "@/lib/request-failure";
import { isRequestCanceled } from "@/lib/http-client";

export type SetupGate =
  | { status: SetupStatus; error: null }
  | { status: null; error: RequestFailure };

export async function setupLoader({
  request,
}: LoaderFunctionArgs): Promise<SetupGate | Response> {
  let status: SetupStatus;
  try {
    status = await getSetup(request.signal);
  } catch (error) {
    if (isRequestCanceled(error)) throw error;
    return { status: null, error: requestFailure(error) };
  }
  const onSetup = new URL(request.url).pathname === "/setup";
  if (status.state !== "completed" && !onSetup) return redirect("/setup");
  if (status.state === "completed" && onSetup) return redirect("/");
  return { status, error: null };
}
