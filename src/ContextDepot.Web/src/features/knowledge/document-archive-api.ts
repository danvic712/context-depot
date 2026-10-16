import {
  responseRecord as record,
  responseText as text,
  responseCount as count,
} from "@/lib/response-values";
import { httpRequest } from "@/lib/http-client";
import { paginationDefaults, type PaginatedResult } from "@/lib/pagination";

export interface ArchivedDocument {
  id: string;
  title: string;
  path: string;
  updatedAt: string;
}
export function parseArchivedDocuments(
  data: unknown,
): PaginatedResult<ArchivedDocument> {
  const value = record(data);
  const page = count(value.page, 1);
  const pageSize = count(value.pageSize, 1);
  const totalCount = count(value.totalCount);
  if (
    !Array.isArray(value.items) ||
    page > paginationDefaults.maximumPage ||
    pageSize > paginationDefaults.maximumPageSize ||
    value.items.length > pageSize ||
    value.items.length > totalCount
  )
    throw new Error("Invalid archived document page");
  const items = value.items.map((data): ArchivedDocument => {
    const item = record(data);
    const document = {
      id: text(item.id),
      title: text(item.title),
      path: text(item.path),
      updatedAt: text(item.updatedAt),
    };
    if (
      !document.id ||
      !document.path ||
      !Number.isFinite(Date.parse(document.updatedAt))
    )
      throw new Error("Invalid archived document");
    return document;
  });
  if (new Set(items.map((item) => item.id)).size !== items.length)
    throw new Error("Duplicate archived document");
  return { items, totalCount, page, pageSize };
}
export function getArchivedDocuments(
  spaceId: string,
  page: number,
  pageSize: number,
  signal: AbortSignal,
) {
  return httpRequest({
    url: `/workspaces/${encodeURIComponent(spaceId)}/archived-documents`,
    params: { page, pageSize },
    signal,
    parse: parseArchivedDocuments,
  });
}
