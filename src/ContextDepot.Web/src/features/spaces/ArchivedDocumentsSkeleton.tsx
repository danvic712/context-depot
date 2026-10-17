import { Skeleton } from "@/components/ui/skeleton";

export function ArchivedDocumentsSkeleton({ count = 6 }: { count?: number }) {
  return (
    <ul className="archived-document-list" aria-hidden="true">
      {Array.from({ length: count }, (_, index) => (
        <li key={index}>
          <div className="archived-document-skeleton">
            <Skeleton className="size-5 shrink-0" />
            <div className="archived-document-skeleton-copy">
              <Skeleton className="h-4 w-3/4" />
              <Skeleton className="h-3 w-1/2" />
              <Skeleton className="h-3 w-28" />
            </div>
          </div>
          <Skeleton className="size-8" />
        </li>
      ))}
    </ul>
  );
}
