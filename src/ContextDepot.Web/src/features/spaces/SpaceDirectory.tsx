import { useEffect, useLayoutEffect, useState } from "react";
import { FolderIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { useLocation } from "react-router";
import { RequestFeedback } from "@/components/feedback/RequestFeedback";
import {
  Empty,
  EmptyHeader,
  EmptyTitle,
  EmptyDescription,
  EmptyMedia,
  EmptyContent,
} from "@/components/ui/empty";
import { Button } from "@/components/ui/button";
import { WorkspaceRow, WorkspaceRowSkeleton } from "./WorkspaceRow";
import type { useDirectoryPage } from "./use-space-directory";
import { directoryParams, hasDirectoryFilters } from "./directory-query";
import { directoryScrollStorageKey } from "./directory-enums";
import { paginationDefaults } from "@/lib/pagination";
import {
  SpaceDirectoryHeading,
  SpaceDirectoryFooter,
  WorkspaceListPanel,
} from "./SpaceDirectoryLayout";

export function SpaceListSkeleton({
  count = paginationDefaults.pageSize,
}: {
  count?: number;
}) {
  return (
    <>
      {Array.from({ length: count }, (_, index) => (
        <li key={index} className="workspace-skeleton-entry" aria-hidden="true">
          <WorkspaceRowSkeleton />
        </li>
      ))}
    </>
  );
}

export function SpaceDirectory({
  directory,
}: {
  directory: ReturnType<typeof useDirectoryPage>;
}) {
  const { t } = useTranslation();
  const location = useLocation();
  const [viewport, setViewport] = useState<HTMLDivElement | null>(null);
  const scopeKey = directoryParams(directory.filters).toString();
  const { resource } = directory;
  const { items, totalCount, page, pageSize, loading, error } = resource;
  const initial = loading && items.length === 0;
  const filtered = hasDirectoryFilters(directory.filters);
  const initialFailure = !!error;
  useLayoutEffect(() => {
    viewport?.scrollTo({ top: 0 });
    if (viewport) window.scrollTo({ top: 0 });
  }, [viewport, scopeKey, page, initial]);
  useEffect(() => {
    if (loading || error || !viewport || items.length === 0) return;
    try {
      const saved = sessionStorage.getItem(directoryScrollStorageKey);
      if (!saved) return;
      const position = JSON.parse(saved);
      if (position.url !== location.pathname + location.search) return;
      sessionStorage.removeItem(directoryScrollStorageKey);
      if (
        typeof position.top === "number" &&
        Number.isFinite(position.top) &&
        position.top >= 0
      ) {
        const frame = requestAnimationFrame(() => {
          window.scrollTo({ top: position.top });
          if (
            typeof position.listTop === "number" &&
            Number.isFinite(position.listTop) &&
            position.listTop >= 0
          )
            viewport?.scrollTo({ top: position.listTop });
        });
        return () => cancelAnimationFrame(frame);
      }
    } catch {
      /* Directory restoration remains available when storage is blocked. */
    }
  }, [
    loading,
    error,
    page,
    items.length,
    location.pathname,
    location.search,
    viewport,
  ]);
  return (
    <section
      className="space-directory"
      aria-labelledby="space-directory-title"
      aria-busy={loading}
    >
      <SpaceDirectoryHeading filtered={filtered} />
      {initial && (
        <span className="sr-only" role="status">
          {t("loading")}
        </span>
      )}
      {initialFailure && (
        <RequestFeedback
          title={t("spacesLoadError")}
          failure={error}
          onRetry={resource.retry}
          pending={loading}
          stale={items.length > 0}
          compact={items.length > 0}
          recoveryAction={
            error?.kind === "invalidQuery"
              ? { label: t("spacesResetFilters"), onClick: directory.reset }
              : undefined
          }
        />
      )}
      {totalCount === 0 && !loading && !error && (
        <Empty className="spaces-empty">
          <EmptyHeader>
            <EmptyMedia className="spaces-empty-art">
              <FolderIcon aria-hidden="true" />
            </EmptyMedia>
            <EmptyTitle>
              {t(filtered ? "spacesNoMatches" : "spacesEmptyTitle")}
            </EmptyTitle>
            <EmptyDescription>
              {t(filtered ? "spacesNoMatchesWhy" : "spacesEmptyDescription")}
            </EmptyDescription>
          </EmptyHeader>
          {filtered && (
            <EmptyContent>
              <Button variant="outline" onClick={directory.reset}>
                {t("spacesResetFilters")}
              </Button>
            </EmptyContent>
          )}
        </Empty>
      )}
      {(initial || items.length > 0) && (
        <WorkspaceListPanel ref={setViewport}>
          <ul
            key={initial ? "loading" : `${scopeKey}:${page}`}
            className="workspace-list"
            data-loading={initial || undefined}
            onKeyDown={(event) => {
              if (event.altKey || event.ctrlKey || event.metaKey) return;
              const link = (
                event.target as HTMLElement
              ).closest<HTMLAnchorElement>("a.workspace-row");
              if (!link) return;
              const links = Array.from(
                event.currentTarget.querySelectorAll<HTMLAnchorElement>(
                  "a.workspace-row",
                ),
              );
              const index = links.indexOf(link);
              const next =
                event.key === "ArrowDown"
                  ? Math.min(index + 1, links.length - 1)
                  : event.key === "ArrowUp"
                    ? Math.max(index - 1, 0)
                    : event.key === "Home"
                      ? 0
                      : event.key === "End"
                        ? links.length - 1
                        : undefined;
              if (next !== undefined) {
                event.preventDefault();
                links[next]?.focus();
              }
            }}
          >
            {initial && (
              <SpaceListSkeleton
                count={
                  totalCount === undefined
                    ? pageSize
                    : Math.min(
                        pageSize,
                        Math.max(0, totalCount - (page - 1) * pageSize),
                      )
                }
              />
            )}
            {items.map((space) => (
              <li className="workspace-list-entry" key={space.id}>
                <WorkspaceRow space={space} directoryPage={page} />
              </li>
            ))}
          </ul>
        </WorkspaceListPanel>
      )}
      <SpaceDirectoryFooter
        totalCount={totalCount}
        page={page}
        pageSize={pageSize}
        itemCount={items.length}
        loading={initial}
        onPageChange={directory.changePage}
        onPageSizeChange={directory.changePageSize}
      />
    </section>
  );
}
