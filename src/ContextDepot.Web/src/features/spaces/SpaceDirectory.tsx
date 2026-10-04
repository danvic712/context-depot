import { useEffect, useRef } from "react";
import { ChevronLeftIcon, ChevronRightIcon, FolderIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";
import { WorkspaceRow, WorkspaceRowSkeleton } from "./WorkspaceRow";
import { RequestFeedback } from "@/components/feedback/RequestFeedback";
import {
  Empty,
  EmptyHeader,
  EmptyTitle,
  EmptyDescription,
  EmptyMedia,
} from "@/components/ui/empty";
import type { useSpaceDirectory } from "./use-space-directory";

export function SpaceListSkeleton({ count = 4 }: { count?: number }) {
  return (
    <>
      {Array.from({ length: count }, (_, i) => (
        <li key={i} aria-hidden="true">
          <WorkspaceRowSkeleton />
        </li>
      ))}
    </>
  );
}

export function SpaceDirectory({
  resource,
}: {
  resource: ReturnType<typeof useSpaceDirectory>;
}) {
  const { t, i18n } = useTranslation();
  const data =
    resource.error && !resource.error.retryable ? undefined : resource.data;
  const pages = Math.max(
    1,
    Math.ceil((data?.totalCount ?? 0) / (data?.pageSize ?? 12)),
  );
  const heading = useRef<HTMLHeadingElement>(null);
  const previousPage = useRef<number | undefined>(undefined);
  const loadedPage = data?.page;
  useEffect(() => {
    if (loadedPage === undefined) return;
    if (
      previousPage.current !== undefined &&
      previousPage.current !== loadedPage &&
      document.activeElement === document.body
    )
      heading.current?.focus({ preventScroll: true });
    previousPage.current = loadedPage;
  }, [loadedPage]);
  const initial = !data && resource.pending && !resource.error;
  return (
    <section
      className="space-directory"
      aria-labelledby="space-directory-title"
      aria-busy={resource.pending}
    >
      <div className="spaces-section-heading">
        <h2 id="space-directory-title" ref={heading} tabIndex={-1}>
          {t("spacesAllSpaces")}
          {data && (
            <span className="spaces-directory-total">
              {new Intl.NumberFormat(i18n.resolvedLanguage).format(
                data.totalCount,
              )}
            </span>
          )}
        </h2>
        <span className="spaces-section-note">{t("spacesDirectoryOrder")}</span>
      </div>
      {initial && (
        <span className="sr-only" role="status">
          {t("loading")}
        </span>
      )}
      {resource.error && (
        <RequestFeedback
          title={t("spacesLoadError")}
          failure={resource.error}
          onRetry={resource.refresh}
          pending={resource.pending}
          stale={!!data}
          compact={!!data}
        />
      )}
      {data?.totalCount === 0 && (
        <Empty className="spaces-empty">
          <EmptyHeader>
            <EmptyMedia className="spaces-empty-art">
              <FolderIcon aria-hidden="true" />
            </EmptyMedia>
            <EmptyTitle>
              <h3>{t("spacesEmptyTitle")}</h3>
            </EmptyTitle>
            <EmptyDescription>{t("spacesEmptyDescription")}</EmptyDescription>
          </EmptyHeader>
        </Empty>
      )}
      {data && !data.items.length && data.totalCount > 0 && (
        <Empty className="spaces-empty">
          <EmptyHeader>
            <EmptyTitle>{t("spacesPageEmpty")}</EmptyTitle>
            <EmptyDescription>
              {t("spacesPageEmptyDescription")}
            </EmptyDescription>
          </EmptyHeader>
          <Button variant="outline" onClick={() => resource.changePage(1)}>
            {t("spacesFirstPage")}
          </Button>
        </Empty>
      )}
      {(initial || !!data?.items.length) && (
        <div className="workspace-list-panel">
          <div className="workspace-list-labels" aria-hidden="true">
            <span>{t("spacesColumnSpace")}</span>
            <span>{t("spacesColumnContents")}</span>
            <span>{t("spacesActivity")}</span>
          </div>
          <ul className="workspace-list">
            {initial && <SpaceListSkeleton />}
            {data?.items.map((space) => (
              <li key={space.id}>
                <WorkspaceRow space={space} />
              </li>
            ))}
          </ul>
        </div>
      )}
      {data && pages > 1 && (
        <nav className="spaces-pagination" aria-label={t("spacesPagination")}>
          <span role="status">
            {t("spacesPage", { page: resource.page, pages })}
          </span>
          <div>
            <Button
              variant="outline"
              size="sm"
              disabled={resource.page <= 1 || resource.pending}
              onClick={() => resource.changePage(resource.page - 1)}
            >
              <ChevronLeftIcon aria-hidden="true" />
              {t("spacesPrevious")}
            </Button>
            <Button
              variant="outline"
              size="sm"
              disabled={resource.page >= pages || resource.pending}
              onClick={() => resource.changePage(resource.page + 1)}
            >
              {t("spacesNext")}
              <ChevronRightIcon aria-hidden="true" />
            </Button>
          </div>
        </nav>
      )}
    </section>
  );
}
