import { PageHeaderSkeleton } from "@/components/content/PageHeader";
import { Skeleton } from "@/components/ui/skeleton";
import { WorkspaceCardSkeleton } from "./WorkspaceCard";
import "@/styles/spaces.css";

export function SpacesSkeleton({ detail = false }: { detail?: boolean }) {
  return (
    <div className="spaces-workbench spaces-page-skeleton" aria-hidden="true">
      {detail && <Skeleton className="h-4 w-48" />}
      <PageHeaderSkeleton />
      {detail && (
        <div className="space-knowledge">
          <Skeleton className="h-16 w-2/3" />
          <Skeleton className="h-5 w-24" />
        </div>
      )}
      <div className="spaces-section-heading">
        <Skeleton className="h-5 w-28" />
        <Skeleton className="h-4 w-24" />
      </div>
      <div className="workspace-grid">
        {Array.from({ length: 4 }, (_, index) => (
          <WorkspaceCardSkeleton key={index} />
        ))}
      </div>
    </div>
  );
}
