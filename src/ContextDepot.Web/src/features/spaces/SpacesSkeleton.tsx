import { PageHeaderSkeleton } from "@/components/content/PageHeader";
import { Skeleton } from "@/components/ui/skeleton";
import { WorkspaceRowSkeleton } from "./WorkspaceRow";
import { SpaceKnowledgeSkeleton } from "./SpaceKnowledgeList";
import "@/styles/spaces.css";

export function SpacesSkeleton({ detail = false }: { detail?: boolean }) {
  return (
    <div
      className={`spaces-workbench spaces-page-skeleton ${detail ? "" : "spaces-directory-page"}`}
      aria-hidden="true"
    >
      <div className={detail ? "space-browser" : undefined}>
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
          <div className="spaces-section-heading">
            <Skeleton className="h-5 w-28" />
            <Skeleton className="h-4 w-24" />
          </div>
          {detail ? (
            <SpaceKnowledgeSkeleton />
          ) : (
            <div className="workspace-list-panel">
              {Array.from({ length: 4 }, (_, index) => (
                <WorkspaceRowSkeleton key={index} />
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
