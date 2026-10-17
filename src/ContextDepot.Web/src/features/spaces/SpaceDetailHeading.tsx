import {
  PageHeader,
  PageHeaderSkeleton,
} from "@/components/content/PageHeader";
import type { WorkspaceSummary } from "./workspace-summary";

export function SpaceDetailHeading({ space }: { space: WorkspaceSummary }) {
  return (
    <div className="space-detail-heading">
      <PageHeader title={space.name} description={space.description ?? ""} />
    </div>
  );
}

export function SpaceDetailHeadingSkeleton() {
  return (
    <div className="space-detail-heading" aria-hidden="true">
      <PageHeaderSkeleton eyebrow={false} />
    </div>
  );
}
