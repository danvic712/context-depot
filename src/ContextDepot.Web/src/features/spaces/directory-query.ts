import {
  DirectoryParameter,
  WorkspaceContentStatus,
  WorkspaceContentType,
  WorkspaceSortOrder,
  WorkspaceUpdateWindow,
} from "./directory-enums";
import { paginationDefaults, readPageSize } from "@/lib/pagination";

export interface DirectoryFilters {
  query: string;
  status: WorkspaceContentStatus;
  content: WorkspaceContentType;
  updated: WorkspaceUpdateWindow;
  from: string;
  to: string;
  sort: WorkspaceSortOrder;
}
export const maximumDirectoryQueryLength = 200;
export const defaultDirectoryFilters: DirectoryFilters = {
  query: "",
  status: WorkspaceContentStatus.All,
  content: WorkspaceContentType.All,
  updated: WorkspaceUpdateWindow.All,
  from: "",
  to: "",
  sort: WorkspaceSortOrder.Activity,
};
function choice<T extends string>(
  value: string | null,
  options: readonly T[],
  fallback: T,
): T {
  return options.find((option) => option === value) ?? fallback;
}
function date(value: string | null) {
  if (!value || !/^\d{4}-\d{2}-\d{2}$/.test(value)) return "";
  const parsed = new Date(`${value}T00:00:00Z`);
  return Number.isFinite(parsed.getTime()) &&
    parsed.toISOString().slice(0, 10) === value
    ? value
    : "";
}
export function readDirectoryFilters(
  params: URLSearchParams,
): DirectoryFilters {
  const status = choice(
    params.get(DirectoryParameter.Status),
    Object.values(WorkspaceContentStatus),
    WorkspaceContentStatus.All,
  );
  const updated = choice(
    params.get(DirectoryParameter.Updated),
    Object.values(WorkspaceUpdateWindow),
    WorkspaceUpdateWindow.All,
  );
  return {
    query: (params.get(DirectoryParameter.Keyword) ?? "")
      .trim()
      .slice(0, maximumDirectoryQueryLength),
    status,
    content:
      status === WorkspaceContentStatus.Empty
        ? WorkspaceContentType.All
        : choice(
            params.get(DirectoryParameter.Content),
            Object.values(WorkspaceContentType),
            WorkspaceContentType.All,
          ),
    updated,
    from:
      updated === WorkspaceUpdateWindow.Custom
        ? date(params.get(DirectoryParameter.From))
        : "",
    to:
      updated === WorkspaceUpdateWindow.Custom
        ? date(params.get(DirectoryParameter.To))
        : "",
    sort: choice(
      params.get(DirectoryParameter.Sort),
      Object.values(WorkspaceSortOrder),
      WorkspaceSortOrder.Activity,
    ),
  };
}
export function hasDirectoryFilters(filters: DirectoryFilters) {
  return (
    !!filters.query ||
    filters.status !== WorkspaceContentStatus.All ||
    filters.content !== WorkspaceContentType.All ||
    filters.updated !== WorkspaceUpdateWindow.All
  );
}
export function directoryParams(
  filters: DirectoryFilters,
  page = 1,
  pageSize: number = paginationDefaults.pageSize,
) {
  const params = new URLSearchParams();
  if (filters.query.trim())
    params.set(DirectoryParameter.Keyword, filters.query.trim());
  if (filters.status !== WorkspaceContentStatus.All)
    params.set(DirectoryParameter.Status, filters.status);
  if (
    filters.content !== WorkspaceContentType.All &&
    filters.status !== WorkspaceContentStatus.Empty
  )
    params.set(DirectoryParameter.Content, filters.content);
  if (filters.updated !== WorkspaceUpdateWindow.All)
    params.set(DirectoryParameter.Updated, filters.updated);
  if (filters.updated === WorkspaceUpdateWindow.Custom) {
    if (filters.from) params.set(DirectoryParameter.From, filters.from);
    if (filters.to) params.set(DirectoryParameter.To, filters.to);
  }
  if (filters.sort !== WorkspaceSortOrder.Activity)
    params.set(DirectoryParameter.Sort, filters.sort);
  if (page > 1) params.set(DirectoryParameter.Page, String(page));
  if (pageSize !== paginationDefaults.pageSize)
    params.set(DirectoryParameter.PageSize, String(pageSize));
  return params;
}
export function directoryRequest(filters: DirectoryFilters) {
  function boundary(value: string, nextDay = false) {
    if (!value) return undefined;
    const result = new Date(`${value}T00:00:00`);
    if (nextDay) result.setDate(result.getDate() + 1);
    return result.toISOString();
  }
  return {
    query: filters.query || undefined,
    status:
      filters.status === WorkspaceContentStatus.All
        ? undefined
        : filters.status,
    content:
      filters.content === WorkspaceContentType.All
        ? undefined
        : filters.content,
    updatedWithinDays: [
      WorkspaceUpdateWindow.Week,
      WorkspaceUpdateWindow.Month,
      WorkspaceUpdateWindow.Quarter,
    ].includes(filters.updated)
      ? Number(filters.updated)
      : undefined,
    updatedFrom:
      filters.updated === WorkspaceUpdateWindow.Custom
        ? boundary(filters.from)
        : undefined,
    updatedBefore:
      filters.updated === WorkspaceUpdateWindow.Custom
        ? boundary(filters.to, true)
        : undefined,
    sort:
      filters.sort === WorkspaceSortOrder.Activity ? undefined : filters.sort,
  };
}
export function directoryPage(params: URLSearchParams) {
  const requested = Number(params.get(DirectoryParameter.Page) ?? 1);
  return Number.isSafeInteger(requested) &&
    requested > 0 &&
    requested <= paginationDefaults.maximumPage
    ? requested
    : 1;
}
export function workspaceDirectoryParams(
  params: URLSearchParams,
  detail = false,
) {
  const source = detail
    ? new URLSearchParams(params.get(DirectoryParameter.ReturnQuery) ?? "")
    : params;
  if (detail && params.has(DirectoryParameter.ReturnPage))
    source.set(
      DirectoryParameter.Page,
      params.get(DirectoryParameter.ReturnPage)!,
    );
  return directoryParams(
    readDirectoryFilters(source),
    directoryPage(source),
    readPageSize(source.get(DirectoryParameter.PageSize)),
  );
}
export function workspaceDetailTarget(id: string, directory: URLSearchParams) {
  const params = new URLSearchParams();
  const page = directory.get(DirectoryParameter.Page);
  if (page) params.set(DirectoryParameter.ReturnPage, page);
  if (
    Array.from(directory.keys()).some((key) => key !== DirectoryParameter.Page)
  )
    params.set(DirectoryParameter.ReturnQuery, directory.toString());
  return `/spaces/${encodeURIComponent(id)}${params.size ? `?${params}` : ""}`;
}
