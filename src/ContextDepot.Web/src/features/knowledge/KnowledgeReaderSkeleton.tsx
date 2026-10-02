import { Skeleton } from "@/components/ui/skeleton";
import "@/styles/knowledge-reader.css";

export function KnowledgeContentSkeleton() {
  return (
    <div className="knowledge-content-skeleton" aria-hidden="true">
      <Skeleton className="h-5 w-16" />
      <Skeleton className="reader-title-skeleton" />
      <Skeleton className="h-4 w-2/3" />
      <div className="reader-body-skeleton">
        <Skeleton className="h-6 w-1/2" />
        <Skeleton className="h-4 w-full" />
        <Skeleton className="h-4 w-full" />
        <Skeleton className="h-4 w-3/4" />
        <Skeleton className="mt-4 h-32 w-full" />
      </div>
    </div>
  );
}

export function KnowledgeReaderSkeleton({
  label = "Loading / 加载中",
}: {
  label?: string;
}) {
  return (
    <section className="knowledge-reader" role="status" aria-label={label}>
      <div className="knowledge-reader-toolbar" aria-hidden="true">
        <Skeleton className="h-9 w-32" />
        <Skeleton className="h-9 w-28" />
      </div>
      <KnowledgeContentSkeleton />
    </section>
  );
}
