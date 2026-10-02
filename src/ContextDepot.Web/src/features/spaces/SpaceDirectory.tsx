import { useEffect, useRef, type ReactNode } from "react";
import {
  ChevronLeftIcon,
  ChevronRightIcon,
  FolderIcon,
  FolderTreeIcon,
} from "lucide-react";
import { Link, useLocation } from "react-router";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { RequestFeedback } from "@/components/feedback/RequestFeedback";
import {
  Empty,
  EmptyHeader,
  EmptyTitle,
  EmptyDescription,
  EmptyMedia,
} from "@/components/ui/empty";
import type { Space } from "./spaces-api";
import type { useSpaceDirectory } from "./use-space-directory";

export function SpaceCounts({ space }: { space: Space }) {
  const { t, i18n } = useTranslation();
  const number = new Intl.NumberFormat(i18n.resolvedLanguage);
  return (
    <div className="space-counts" title={t("spaceCountsHint")}>
      <span>
        {t("spacesContextCount", {
          count: space.contextCount,
          value: number.format(space.contextCount),
        })}
      </span>
      <span>
        {t("spacesDocumentCount", {
          count: space.documentCount,
          value: number.format(space.documentCount),
        })}
      </span>
    </div>
  );
}

function SpaceCard({ space }: { space: Space }) {
  const { t } = useTranslation();
  const location = useLocation();
  return (
    <li>
      <Link
        className="space-card"
        to={`/spaces/${space.id}`}
        state={{
          from: location.pathname + location.search,
          navigation: "spaces",
        }}
      >
        <div className="space-card-top">
          <span className="space-card-icon">
            <FolderIcon aria-hidden="true" />
          </span>
          {space.subspaceCount > 0 && (
            <span className="space-child-count">
              <FolderTreeIcon aria-hidden="true" />
              {t("spacesSubspaceCount", { count: space.subspaceCount })}
            </span>
          )}
        </div>
        <h3 title={space.name}>{space.name}</h3>
        <p className="space-card-description">
          {space.description || t("spaceNoDescription")}
        </p>
        <div className="space-card-footer">
          <span className="space-card-path" title={space.path}>
            {space.path}
          </span>
          <SpaceCounts space={space} />
          <ChevronRightIcon className="space-card-arrow" aria-hidden="true" />
        </div>
      </Link>
    </li>
  );
}

export function SpaceGridSkeleton({ count = 4 }: { count?: number }) {
  return (
    <>
      {Array.from({ length: count }, (_, i) => (
        <li key={i} aria-hidden="true">
          <div className="space-card space-card-skeleton">
            <Skeleton className="size-9 rounded-lg" />
            <Skeleton className="mt-3 h-5 w-2/3" />
            <Skeleton className="mt-2 h-4 w-full" />
            <div className="space-card-footer">
              <Skeleton className="h-3 w-1/2" />
              <Skeleton className="mt-2 h-3 w-3/4" />
            </div>
          </div>
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
        <ul className="space-grid">
          {initial && <SpaceGridSkeleton count={root ? 3 : 4} />}
          {resource.data?.items.map((space) => (
            <SpaceCard key={space.id} space={space} />
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
