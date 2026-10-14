import { PageHeaderSkeleton } from "@/components/content/PageHeader";
import { Skeleton } from "@/components/ui/skeleton";
import { SpaceKnowledgeSkeleton } from "./SpaceKnowledgeList";
import {
  SpaceDirectoryFooter,
  WorkspaceListPanel,
} from "./SpaceDirectoryLayout";
import { SpaceListSkeleton } from "./SpaceDirectory";
import { SpaceDirectoryFiltersSkeleton } from "./SpaceDirectoryFilters";
import { readPageSize } from "@/lib/pagination";
import { DirectoryParameter } from "./directory-enums";
import { readDirectoryFilters } from "./directory-query";
import "@/styles/spaces.css";

export function SpacesSkeleton({
  detail = false,
  search = "",
}: {
  detail?: boolean;
  search?: string;
}) {
  if (!detail) {
    const params = new URLSearchParams(search);
    const filters = readDirectoryFilters(params);
    const pageSize = readPageSize(params.get(DirectoryParameter.PageSize));
    return (
      <div
        className="spaces-workbench spaces-directory-page spaces-page-skeleton"
        aria-hidden="true"
        inert
      >
        <SpaceDirectoryFiltersSkeleton filters={filters} />
        <section className="space-directory">
          <WorkspaceListPanel>
            <ul className="workspace-list">
              <SpaceListSkeleton count={pageSize} />
            </ul>
          </WorkspaceListPanel>
          <SpaceDirectoryFooter loading pageSize={pageSize} />
        </section>
      </div>
    );
  }
  return (
    <div
      className={`spaces-workbench spaces-page-skeleton ${detail ? "" : "spaces-directory-page"}`}
      aria-hidden="true"
    >
      <div className="space-browser">
        {detail && (
          <aside className="space-browser-sidebar">
            <div className="space-navigation">
              <Skeleton className="h-11 w-full" />
              {Array.from({ length: 4 }, (_, index) => (
                <Skeleton key={index} className="space-navigation-skeleton" />
              ))}
            </div>
          </aside>
        )}
        <div className="space-browser-main">
          {detail && (
            <Skeleton className="space-browser-mobile-trigger h-11 w-32" />
          )}
          {detail && <Skeleton className="h-4 w-48" />}
          <PageHeaderSkeleton />
          <div className="spaces-section-heading" aria-hidden="true">
            <Skeleton className="h-5 w-28" />
            <Skeleton className="h-4 w-24" />
          </div>
          <SpaceKnowledgeSkeleton />
        </div>
      </div>
    </div>
  );
}
