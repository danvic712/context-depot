export const paginationDefaults = {
  pageSize: 12,
  maximumPage: 100000,
  maximumPageSize: 60,
  pageSizes: [6, 12, 24, 48] as readonly number[],
} as const;

export interface PaginatedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export function paginationPages(
  page: number,
  pageCount: number,
): (number | "start-gap" | "end-gap")[] {
  if (pageCount <= 0) return [];
  const current = Math.min(Math.max(1, page), pageCount);
  if (pageCount <= 7)
    return Array.from({ length: pageCount }, (_, index) => index + 1);
  if (current <= 4) return [1, 2, 3, 4, 5, "end-gap", pageCount];
  if (current >= pageCount - 3)
    return [
      1,
      "start-gap",
      pageCount - 4,
      pageCount - 3,
      pageCount - 2,
      pageCount - 1,
      pageCount,
    ];
  return [
    1,
    "start-gap",
    current - 1,
    current,
    current + 1,
    "end-gap",
    pageCount,
  ];
}

export function readPageSize(value: string | null) {
  const requested = Number(value ?? paginationDefaults.pageSize);
  return Number.isSafeInteger(requested) &&
    requested >= 1 &&
    requested <= paginationDefaults.maximumPageSize
    ? requested
    : paginationDefaults.pageSize;
}
