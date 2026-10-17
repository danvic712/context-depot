import { useId, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { ChevronLeftIcon, ChevronRightIcon } from "lucide-react";
import {
  Pagination,
  PaginationContent,
  PaginationItem,
  PaginationLink,
  PaginationPrevious,
  PaginationNext,
  PaginationEllipsis,
} from "@/components/ui/pagination";
import { AppSelect } from "@/components/ui/AppSelect";
import { Field, FieldLabel } from "@/components/ui/field";
import { Skeleton } from "@/components/ui/skeleton";
import { paginationDefaults, paginationPages } from "@/lib/pagination";
import { cn } from "@/lib/utils";
import configuredI18n from "@/lib/i18n";
import "@/styles/pagination.css";

export interface PaginationBarProps {
  page?: number;
  pageSize?: number;
  totalCount?: number;
  itemCount?: number;
  loading?: boolean;
  disabled?: boolean;
  compact?: boolean;
  summary?: ReactNode;
  ariaLabel?: string;
  className?: string;
  pageSizeOptions?: readonly number[];
  onPageChange?: (page: number) => void;
  onPageSizeChange?: (pageSize: number) => void;
}

export function PaginationBar({
  page = 1,
  pageSize = paginationDefaults.pageSize,
  totalCount,
  itemCount = 0,
  loading = false,
  disabled = false,
  compact = false,
  summary,
  ariaLabel,
  className,
  pageSizeOptions = paginationDefaults.pageSizes,
  onPageChange,
  onPageSizeChange,
}: PaginationBarProps) {
  const { t, i18n, ready } = useTranslation(undefined, {
    i18n: configuredI18n,
    useSuspense: false,
  });
  const pageSizeId = useId();
  const number = new Intl.NumberFormat(i18n.resolvedLanguage);
  const pages = Math.ceil((totalCount ?? 0) / pageSize);
  const from = (page - 1) * pageSize + 1;
  const to = Math.min(from + itemCount - 1, totalCount ?? 0);
  const sizes = [...new Set([...pageSizeOptions, pageSize])]
    .filter(
      (value) =>
        Number.isSafeInteger(value) &&
        value >= 1 &&
        value <= paginationDefaults.maximumPageSize,
    )
    .sort((a, b) => a - b);
  if (!ready)
    return (
      <div
        className={cn(
          "pagination-bar",
          compact && "pagination-bar-compact",
          className,
        )}
        aria-hidden="true"
      >
        {!compact && <Skeleton className="pagination-summary-skeleton" />}
        <div className="pagination-controls">
          <Skeleton className="pagination-navigation-skeleton" />
        </div>
      </div>
    );
  function changePage(next: number) {
    if (!disabled && next >= 1 && next <= pages && next !== page)
      onPageChange?.(next);
  }
  return (
    <div
      className={cn(
        "pagination-bar",
        compact && "pagination-bar-compact",
        className,
      )}
    >
      {!compact &&
        (loading ? (
          <Skeleton className="pagination-summary-skeleton" />
        ) : totalCount !== undefined ? (
          <span
            className="pagination-summary"
            role="status"
            aria-live="polite"
            aria-atomic="true"
          >
            {summary ??
              (itemCount > 0
                ? t("paginationRange", {
                    from: number.format(from),
                    to: number.format(to),
                    total: number.format(totalCount),
                  })
                : t("paginationTotal", { total: number.format(totalCount) }))}
          </span>
        ) : null)}
      <div className="pagination-controls">
        {!compact &&
          (onPageSizeChange ? (
            <Field orientation="horizontal" className="pagination-page-size">
              <FieldLabel htmlFor={pageSizeId}>
                {t("paginationPageSize")}
              </FieldLabel>
              <AppSelect
                id={pageSizeId}
                label={t("paginationPageSize")}
                value={String(pageSize)}
                disabled={disabled}
                options={sizes.map((value) => ({
                  value: String(value),
                  label: number.format(value),
                }))}
                onChange={(value) => onPageSizeChange(Number(value))}
              />
            </Field>
          ) : loading && totalCount === undefined ? (
            <Skeleton className="pagination-size-skeleton" />
          ) : null)}
        {pages > 1 && onPageChange ? (
          <Pagination aria-label={ariaLabel ?? t("paginationLabel")}>
            <PaginationContent>
              <PaginationItem>
                <PaginationPrevious
                  asChild
                  size={compact ? "icon" : "default"}
                  aria-label={t("paginationPrevious")}
                >
                  <button
                    type="button"
                    disabled={disabled || page <= 1}
                    onClick={() => changePage(page - 1)}
                  >
                    <ChevronLeftIcon
                      data-icon="inline-start"
                      aria-hidden="true"
                    />
                    <span className={compact ? "sr-only" : "hidden sm:block"}>
                      {t("paginationPrevious")}
                    </span>
                  </button>
                </PaginationPrevious>
              </PaginationItem>
              {compact ? (
                <PaginationItem>
                  <span className="pagination-position" role="status">
                    {t("paginationPage", {
                      page: number.format(page),
                      pages: number.format(pages),
                    })}
                  </span>
                </PaginationItem>
              ) : (
                paginationPages(page, pages).map((value) => (
                  <PaginationItem
                    key={value}
                    className="pagination-page-item"
                    data-current={value === page || undefined}
                  >
                    {typeof value === "number" ? (
                      <PaginationLink
                        asChild
                        isActive={value === page}
                        aria-label={t("paginationGoToPage", { page: value })}
                      >
                        <button
                          type="button"
                          disabled={disabled}
                          onClick={() => changePage(value)}
                        >
                          {number.format(value)}
                        </button>
                      </PaginationLink>
                    ) : (
                      <PaginationEllipsis />
                    )}
                  </PaginationItem>
                ))
              )}
              <PaginationItem>
                <PaginationNext
                  asChild
                  size={compact ? "icon" : "default"}
                  aria-label={t("paginationNext")}
                >
                  <button
                    type="button"
                    disabled={disabled || page >= pages}
                    onClick={() => changePage(page + 1)}
                  >
                    <span className={compact ? "sr-only" : "hidden sm:block"}>
                      {t("paginationNext")}
                    </span>
                    <ChevronRightIcon
                      data-icon="inline-end"
                      aria-hidden="true"
                    />
                  </button>
                </PaginationNext>
              </PaginationItem>
            </PaginationContent>
          </Pagination>
        ) : loading && totalCount === undefined ? (
          <Skeleton className="pagination-navigation-skeleton" />
        ) : null}
      </div>
    </div>
  );
}
