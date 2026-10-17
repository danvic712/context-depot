import { Skeleton } from "@/components/ui/skeleton";
import "@/styles/knowledge-reader.css";

export function KnowledgeReaderTitleSkeleton() {
  return (
    <span className="reader-panel-title-skeleton" aria-hidden="true">
      <Skeleton as="span" className="reader-title-line" />
      <Skeleton as="span" className="reader-title-line" />
    </span>
  );
}

export function KnowledgeReaderMetadataSkeleton() {
  return (
    <div className="reader-metadata-skeleton" aria-hidden="true">
      <Skeleton className="h-5 w-16" />
      <Skeleton className="h-3 w-32" />
    </div>
  );
}

export function KnowledgeContentSkeleton({
  showHeading = true,
}: {
  showHeading?: boolean;
}) {
  return (
    <div className="knowledge-content-skeleton" aria-hidden="true">
      {showHeading && (
        <div className="reader-heading-skeleton">
          <Skeleton className="h-5 w-16" />
          <Skeleton className="reader-title-skeleton" />
          <Skeleton className="h-4 w-2/3" />
        </div>
      )}
      <div
        className={
          showHeading ? "reader-body-skeleton" : "reader-body-placeholder"
        }
      >
        <Skeleton className="reader-section-skeleton" />
        <div className="reader-paragraph-skeleton">
          <Skeleton className="reader-line-skeleton" />
          <Skeleton className="reader-line-skeleton" />
          <Skeleton className="reader-line-skeleton w-4/5" />
        </div>
        <div className="reader-paragraph-skeleton">
          <Skeleton className="reader-line-skeleton" />
          <Skeleton className="reader-line-skeleton w-11/12" />
          <Skeleton className="reader-line-skeleton w-2/3" />
        </div>
        <Skeleton className="reader-section-skeleton w-1/3" />
        <div className="reader-paragraph-skeleton">
          <Skeleton className="reader-line-skeleton" />
          <Skeleton className="reader-line-skeleton w-3/4" />
        </div>
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
      <div className="knowledge-reader-document">
        <KnowledgeContentSkeleton />
      </div>
    </section>
  );
}
