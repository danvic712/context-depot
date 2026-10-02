import { useEffect, useRef, type ReactNode } from "react";
import { ChevronLeftIcon, ChevronRightIcon, FolderIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";
import { WorkspaceCard, WorkspaceCardSkeleton } from "./WorkspaceCard";
import { RequestFeedback } from "@/components/feedback/RequestFeedback";
import {
  Empty,
  EmptyHeader,
  EmptyTitle,
  EmptyDescription,
  EmptyMedia,
} from "@/components/ui/empty";
import type { useSpaceDirectory } from "./use-space-directory";

export function SpaceGridSkeleton({ count = 4 }: { count?: number }) {
  return (
    <>
      {Array.from({ length: count }, (_, i) => (
        <li key={i} aria-hidden="true">
          <WorkspaceCardSkeleton />
        </li>
      ))}
    </>
  );
}

export function SpaceDirectory({
  resource,
  root = false,
  creation,
}: {
  resource: ReturnType<typeof useSpaceDirectory>;
  root?: boolean;
  creation?: ReactNode;
}) {
  const { t } = useTranslation();
  const pages = Math.max(
    1,
    Math.ceil(
      (resource.data?.totalCount ?? 0) / (resource.data?.pageSize ?? 12),
    ),
  );
  const heading = useRef<HTMLHeadingElement>(null);
  const previousPage = useRef<number | undefined>(undefined);
  const loadedPage = resource.data?.page;
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
  const initial = !resource.data && resource.pending && !resource.error;
  return (
    <section
      className="space-directory"
      aria-labelledby="space-directory-title"
      aria-busy={resource.pending}
    >
      <div className="spaces-section-heading">
        <h2 id="space-directory-title" ref={heading} tabIndex={-1}>
          {t(root ? "spacesRootTitle" : "spacesChildrenTitle")}
        </h2>
        <span className="spaces-section-note">
          {t(root ? "spacesRootLabel" : "spacesDirectoryOrder")}
        </span>
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
          stale={!!resource.data}
          compact={!!resource.data}
        />
      )}
      {resource.data?.totalCount === 0 && root && (
        <div className="spaces-empty-note" role="status">
          <strong>{t("spacesEmptyTitle")}</strong>
          <p>{t("spacesEmptyDescription")}</p>
        </div>
      )}
      {resource.data &&
        !resource.data.items.length &&
        !root &&
        resource.data.totalCount === 0 && (
          <Empty className="spaces-empty">
            <EmptyHeader>
              <EmptyMedia className="spaces-empty-art">
                <FolderIcon aria-hidden="true" />
              </EmptyMedia>
              <EmptyTitle>
                <h3>{t("spacesChildrenEmptyTitle")}</h3>
              </EmptyTitle>
              <EmptyDescription>
                {t("spacesChildrenEmptyDescription")}
              </EmptyDescription>
            </EmptyHeader>
          </Empty>
        )}
      {resource.data &&
        !resource.data.items.length &&
        resource.data.totalCount > 0 && (
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
      {(initial || resource.data?.items.length || creation) && (
        <ul className="workspace-grid">
          {initial && <SpaceGridSkeleton count={root ? 3 : 4} />}
          {resource.data?.items.map((space) => (
            <li key={space.id}>
              <WorkspaceCard space={space} navigation="spaces" />
            </li>
          ))}
          {creation && <li>{creation}</li>}
        </ul>
      )}
      {resource.data && pages > 1 && (
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
