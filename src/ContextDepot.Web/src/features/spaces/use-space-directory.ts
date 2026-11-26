import { useDisplayPreference } from "@/lib/display-preferences";
import { useCallback, useEffect, useState } from "react";
import { useLoaderData, useNavigation, useSearchParams } from "react-router";
import { useRequestResource } from "@/hooks/use-request-resource";
import {
  getSpaceDirectory,
  getWorkspaceChildren,
  type SpaceDirectory,
} from "./spaces-api";
import {
  getDirectoryPage,
  type SpaceDirectoryRouteData,
  type SpacePageSeed,
} from "./space-route-loader";
import { useSpacePageResource } from "./use-space-page-resource";
import {
  defaultDirectoryFilters,
  directoryParams,
  directoryPage,
  readDirectoryFilters,
  workspaceDirectoryParams,
  type DirectoryFilters,
} from "./directory-query";
import { DirectoryParameter, WorkspaceSortOrder } from "./directory-enums";
import { paginationDefaults } from "@/lib/pagination";

export function useSpaceDirectory(
  parentId?: string,
  pageKey:
    | DirectoryParameter.Page
    | DirectoryParameter.ReturnPage = DirectoryParameter.Page,
) {
  const [params, setParams] = useSearchParams();
  const directory = workspaceDirectoryParams(
    params,
    pageKey === DirectoryParameter.ReturnPage,
  );
  const serialized = directoryParams(
    readDirectoryFilters(directory),
  ).toString();
  const requestedPage = Number(directory.get(DirectoryParameter.Page) ?? 1);
  const page =
    Number.isSafeInteger(requestedPage) &&
    requestedPage > 0 &&
    requestedPage <= paginationDefaults.maximumPage
      ? requestedPage
      : 1;
  const [requestedPageSize] = useDisplayPreference("directoryPageSize");
  const [attempt, setAttempt] = useState(0);
  const load = useCallback(
    (signal: AbortSignal) =>
      getSpaceDirectory(
        parentId,
        page,
        signal,
        readDirectoryFilters(new URLSearchParams(serialized)),
        requestedPageSize,
      ),
    [parentId, page, serialized, requestedPageSize],
  );
  const resource = useRequestResource(
    `${parentId ?? "root"}:${page}:${requestedPageSize}:${serialized}`,
    attempt,
    load,
  );
  function changePage(value: number) {
    const next = new URLSearchParams(params);
    if (value === 1) next.delete(pageKey);
    else next.set(pageKey, String(value));
    if (
      pageKey === DirectoryParameter.ReturnPage &&
      next.has(DirectoryParameter.ReturnQuery)
    ) {
      const saved = new URLSearchParams(
        next.get(DirectoryParameter.ReturnQuery)!,
      );
      if (value === 1) saved.delete(DirectoryParameter.Page);
      else saved.set(DirectoryParameter.Page, String(value));
      next.set(DirectoryParameter.ReturnQuery, saved.toString());
    }
    setParams(next, { preventScrollReset: true });
  }
  return {
    ...resource,
    page,
    changePage,
    refresh: () => setAttempt((value) => value + 1),
    onCreated: () => {
      changePage(1);
      setAttempt((value) => value + 1);
    },
  };
}

export function useWorkspaceChildren(
  parentId: string,
  page: number,
  sort = WorkspaceSortOrder.Activity,
  seed?: SpacePageSeed<SpaceDirectory>,
) {
  const load = useCallback(
    (signal: AbortSignal) => getWorkspaceChildren(parentId, page, signal, sort),
    [parentId, page, sort],
  );
  return useSpacePageResource(
    `${parentId}:${page}:${sort}`,
    load,
    seed?.page === page ? seed : undefined,
  );
}

export function useDirectoryPage() {
  const [params, setParams] = useSearchParams();
  const navigation = useNavigation();
  const initial = useLoaderData<SpaceDirectoryRouteData | undefined>();
  const filters = readDirectoryFilters(params);
  const signature = directoryParams(filters).toString();
  const page = directoryPage(params);
  const [requestedPageSize, setPageSize] =
    useDisplayPreference("directoryPageSize");
  const [metadata, setMetadata] = useState<{
    signature: string;
    totalCount: number;
    pageSize: number;
  }>();
  const load = useCallback(
    (signal: AbortSignal) => {
      const requestFilters = readDirectoryFilters(
        new URLSearchParams(signature),
      );
      return getDirectoryPage(requestFilters, page, requestedPageSize, signal);
    },
    [signature, page, requestedPageSize],
  );
  const resource = useSpacePageResource(
    `${signature}:${page}:${requestedPageSize}`,
    load,
    initial?.page === page &&
      initial.pageSize === requestedPageSize &&
      initial.signature === signature
      ? initial
      : undefined,
  );
  const data = resource.data;
  // Retain only pagination metadata between pages; rows belong to the current request.
  if (
    data &&
    (metadata?.signature !== signature ||
      metadata.totalCount !== data.totalCount ||
      metadata.pageSize !== data.pageSize)
  ) {
    setMetadata({
      signature,
      totalCount: data.totalCount,
      pageSize: data.pageSize,
    });
  } else if (resource.error && !resource.error.retryable && metadata) {
    setMetadata(undefined);
  }
  useEffect(() => {
    if (!data || resource.pending || resource.error) return;
    const lastPage = Math.max(1, Math.ceil(data.totalCount / data.pageSize));
    const target = directoryParams(
      readDirectoryFilters(new URLSearchParams(signature)),
      Math.min(page, lastPage),
    );
    if (target.toString() !== params.toString())
      setParams(target, { replace: true, preventScrollReset: true });
  }, [
    data,
    resource.pending,
    resource.error,
    signature,
    page,
    requestedPageSize,
    params,
    setParams,
  ]);
  const accessible = !resource.error || resource.error.retryable;
  const totalCount = accessible
    ? (data?.totalCount ??
      (metadata?.signature === signature ? metadata.totalCount : undefined))
    : undefined;
  const pageSize = requestedPageSize;
  function apply(next: DirectoryFilters) {
    const target = directoryParams(next, 1);
    if (target.toString() === params.toString()) resource.retry();
    else setParams(target, { preventScrollReset: true });
  }
  function changePage(value: number) {
    const lastPage = Math.max(1, Math.ceil((totalCount ?? 0) / pageSize));
    if (
      !Number.isSafeInteger(value) ||
      value < 1 ||
      value > lastPage ||
      value === page
    )
      return;
    setParams(directoryParams(filters, value), {
      preventScrollReset: true,
    });
  }
  function changePageSize(value: number) {
    if (
      !Number.isSafeInteger(value) ||
      value < 1 ||
      value > paginationDefaults.maximumPageSize ||
      value === requestedPageSize
    )
      return;
    setPageSize(value);
    setParams(directoryParams(filters, 1), { preventScrollReset: true });
  }
  return {
    resource: {
      items: data?.items ?? [],
      totalCount,
      page,
      pageSize,
      loading: resource.pending,
      error: resource.error,
      pending: resource.pending || navigation.state !== "idle",
      retry: resource.retry,
      onCreated: () => {
        if (
          params.toString() ===
          directoryParams(defaultDirectoryFilters, 1).toString()
        )
          resource.retry();
        else
          setParams(directoryParams(defaultDirectoryFilters, 1), {
            preventScrollReset: true,
          });
      },
    },
    filters,
    changePage,
    changePageSize,
    apply,
    reset: () =>
      apply({ ...filters, ...defaultDirectoryFilters, sort: filters.sort }),
    setSort: (sort: WorkspaceSortOrder) => apply({ ...filters, sort }),
  };
}
