import { useDisplayPreference } from "@/lib/display-preferences";
import { PageHeaderSkeleton } from "@/components/content/PageHeader";
import { Skeleton } from "@/components/ui/skeleton";
import { SpaceKnowledgeSkeleton } from "./SpaceKnowledgeList";
import {
  SpaceDirectoryFooter,
  WorkspaceListPanel,
} from "./SpaceDirectoryLayout";
import { SpaceListSkeleton } from "./SpaceDirectory";
import { SpaceDirectoryFiltersSkeleton } from "./SpaceDirectoryFilters";
import { readDirectoryFilters } from "./directory-query";
import "@/styles/spaces.css";

export function SpacesSkeleton({
  detail = false,
  search = "",
}: {
  detail?: boolean;
  search?: string;
}) {
  const [pageSize] = useDisplayPreference("directoryPageSize");
  if (!detail) {
    const params = new URLSearchParams(search);
    const filters = readDirectoryFilters(params);
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
    <div className="spaces-workbench spaces-page-skeleton" aria-hidden="true">
      <div className="space-browser">
        <div className="space-browser-main">
          <div className="space-detail-navigation">
            <Skeleton className="h-11 w-24" />
          </div>
          <div className="space-detail-heading">
            <PageHeaderSkeleton eyebrow={false} />
          </div>
          <div className="space-content-toolbar" aria-hidden="true">
            <Skeleton className="h-12 w-44" />
            <div className="space-content-actions">
              <Skeleton className="space-layout-switch h-11 w-24" />
              <div className="space-toolbar-commands">
                <Skeleton className="h-11 w-28" />
                <Skeleton className="h-11 w-24" />
              </div>
            </div>
          </div>
          <SpaceKnowledgeSkeleton />
        </div>
      </div>
    </div>
  );
}
