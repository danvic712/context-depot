import type {
  LoaderFunctionArgs,
  ShouldRevalidateFunctionArgs,
} from "react-router";
import type { Resource } from "@/hooks/use-request-resource";
import { readDisplayPreference } from "@/lib/display-preferences";
import { isRequestCanceled } from "@/lib/http-client";
import { paginationDefaults, type PaginatedResult } from "@/lib/pagination";
import { requestFailure } from "@/lib/request-failure";
import {
  getArchivedDocuments,
  type ArchivedDocument,
} from "@/features/knowledge/document-archive-api";
import {
  directoryPage,
  directoryParams,
  hasDirectoryFilters,
  readDirectoryFilters,
  type DirectoryFilters,
} from "./directory-query";
import {
  getSpace,
  getSpaceDirectory,
  getSpaceKnowledge,
  getWorkspaceChildren,
  type SpaceDetail,
  type SpaceDirectory,
  type SpaceKnowledge,
} from "./spaces-api";

export type SpacePageSeed<T> = {
  page: number;
  pageSize: number;
  resource: Resource<T>;
};

type SpacePanel =
  | { view: "knowledge"; seed: SpacePageSeed<SpaceKnowledge> }
  | { view: "children"; seed: SpacePageSeed<SpaceDirectory> }
  | { view: "archive"; seed: SpacePageSeed<PaginatedResult<ArchivedDocument>> };

export type SpaceRouteData = {
  detail: Resource<SpaceDetail>;
  panel?: SpacePanel;
};

export type SpaceDirectoryRouteData = SpacePageSeed<SpaceDirectory> & {
  signature: string;
};

async function settle<T>(request: Promise<T>): Promise<Resource<T>> {
  try {
    return { data: await request, pending: false };
  } catch (error) {
    if (isRequestCanceled(error)) throw error;
    return { error: requestFailure(error), pending: false };
  }
}

export function getDirectoryPage(
  filters: DirectoryFilters,
  page: number,
  pageSize: number,
  signal: AbortSignal,
) {
  return getSpaceDirectory(
    undefined,
    page,
    signal,
    filters,
    pageSize,
    !hasDirectoryFilters(filters),
  ).then((data) => {
    if (data.page !== page || data.pageSize !== pageSize)
      throw new Error("Invalid workspace directory page");
    return data;
  });
}

export async function spaceDirectoryLoader({
  request,
}: LoaderFunctionArgs): Promise<SpaceDirectoryRouteData> {
  const params = new URL(request.url).searchParams;
  const filters = readDirectoryFilters(params);
  const page = directoryPage(params);
  const pageSize = readDisplayPreference("directoryPageSize");
  return {
    signature: directoryParams(filters).toString(),
    page,
    pageSize,
    resource: await settle(
      getDirectoryPage(filters, page, pageSize, request.signal),
    ),
  };
}

export function readSpacePanelPage(params: URLSearchParams, key: string) {
  const value = Number(params.get(key) ?? 1);
  return Number.isSafeInteger(value) &&
    value >= 1 &&
    value <= paginationDefaults.maximumPage
    ? value
    : 1;
}

function requestedPanel(url: URL): SpacePanel["view"] | undefined {
  const segment = url.pathname.split("/").filter(Boolean)[2];
  if (segment === "children") return "children";
  if (segment === "archived") return "archive";
  if (segment === "contexts" || segment === "documents") return undefined;
  return "knowledge";
}

async function loadPanel(
  url: URL,
  id: string,
  signal: AbortSignal,
): Promise<SpacePanel> {
  const view = requestedPanel(url) ?? "knowledge";
  const page = readSpacePanelPage(url.searchParams, `${view}Page`);
  const pageSize =
    view === "children"
      ? paginationDefaults.pageSize
      : readDisplayPreference(`${view}PageSize`);
  if (view === "children") {
    return {
      view,
      seed: {
        page,
        pageSize,
        resource: await settle(getWorkspaceChildren(id, page, signal)),
      },
    };
  }
  if (view === "archive") {
    return {
      view,
      seed: {
        page,
        pageSize,
        resource: await settle(
          getArchivedDocuments(id, page, pageSize, signal),
        ),
      },
    };
  }
  return {
    view,
    seed: {
      page,
      pageSize,
      resource: await settle(getSpaceKnowledge(id, page, signal, pageSize)),
    },
  };
}

export async function spaceRouteLoader({
  params,
  request,
}: LoaderFunctionArgs): Promise<SpaceRouteData> {
  const id = params.spaceId ?? "";
  const [detail, panel] = await Promise.all([
    settle(getSpace(id, request.signal)),
    loadPanel(new URL(request.url), id, request.signal),
  ]);
  return { detail, panel: detail.data ? panel : undefined };
}

export function shouldRevalidateSpaceRoute({
  currentParams,
  nextParams,
  currentUrl,
  nextUrl,
  defaultShouldRevalidate,
}: ShouldRevalidateFunctionArgs): boolean {
  if (currentParams.spaceId !== nextParams.spaceId) return true;
  if (currentUrl.href === nextUrl.href) return defaultShouldRevalidate;
  const target = requestedPanel(nextUrl);
  // An open reader keeps the page underneath it, including the archive if it was opened there.
  return (
    !!target &&
    (target !== requestedPanel(currentUrl) ||
      readSpacePanelPage(currentUrl.searchParams, `${target}Page`) !==
        readSpacePanelPage(nextUrl.searchParams, `${target}Page`))
  );
}
