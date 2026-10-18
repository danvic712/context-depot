import { Field, FieldGroup } from "@/components/ui/field";
import { Skeleton } from "@/components/ui/skeleton";
import "@/styles/search-box.css";
import "@/styles/search.css";

export function SearchResultsSkeleton() {
  return (
    <ul className="search-result-rows" aria-hidden="true">
      {Array.from({ length: 3 }, (_, index) => (
        <li key={index}>
          <div className="search-result-row search-result-skeleton">
            <Skeleton className="search-result-icon" />
            <div className="search-result-text">
              <Skeleton className="search-result-title-skeleton" />
              <div className="search-result-meta">
                <Skeleton className="h-5 w-14" />
                <Skeleton className="h-3 w-12" />
                <Skeleton className="h-3 w-1/3" />
              </div>
              <div className="search-skeleton-lines">
                <Skeleton className="h-3 w-full" />
                <Skeleton className="h-3 w-4/5" />
              </div>
            </div>
            <Skeleton className="search-row-chevron h-4" />
          </div>
        </li>
      ))}
    </ul>
  );
}

export function SearchPreviewSkeleton() {
  return (
    <div className="search-preview-skeleton" aria-hidden="true">
      <div className="search-preview-heading">
        <Skeleton className="h-5 w-16" />
        <Skeleton className="search-preview-title-skeleton" />
        <Skeleton className="h-5 w-2/3" />
      </div>
      <div className="search-skeleton-lines">
        <Skeleton className="mb-3 h-6 w-1/2" />
        <Skeleton className="h-3 w-full" />
        <Skeleton className="h-3 w-full" />
        <Skeleton className="h-3 w-3/4" />
        <Skeleton className="mt-4 h-3 w-full" />
        <Skeleton className="h-3 w-4/5" />
        <Skeleton className="mt-4 h-24 w-full" />
      </div>
    </div>
  );
}

export function SearchPageSkeleton() {
  return (
    <div className="search-workbench search-page-skeleton" aria-hidden="true">
      <div className="search-panes">
        <div className="search-controls-pane">
          <div className="search-filter-heading">
            <Skeleton className="h-5 w-20" />
            <Skeleton className="h-8 w-24" />
          </div>
          <div className="search-controls-scroll">
            <div className="search-box" data-sidebar>
              <div className="search-skeleton-lines">
                <Skeleton className="h-4 w-20" />
                <div className="search-input-group search-input-skeleton">
                  <Skeleton className="size-5 shrink-0" />
                  <Skeleton className="h-4 w-1/2" />
                </div>
                <Skeleton className="search-sidebar-submit" />
              </div>
            </div>
            <div className="search-filter-content">
              <FieldGroup>
                {[0, 1, 2].map((group) => (
                  <Field key={group}>
                    <Skeleton className="h-4 w-20" />
                    {group === 0 ? (
                      <div className="search-type-placeholder">
                        {[0, 1, 2].map((index) => (
                          <Skeleton key={index} />
                        ))}
                      </div>
                    ) : (
                      Array.from(
                        { length: group === 2 ? 1 : 3 },
                        (_, index) => (
                          <Skeleton
                            className="search-filter-skeleton"
                            key={index}
                          />
                        ),
                      )
                    )}
                  </Field>
                ))}
              </FieldGroup>
            </div>
          </div>
        </div>
        <section className="search-results-pane">
          <div className="search-pane-toolbar">
            <Skeleton className="h-5 w-24" />
            <Skeleton className="h-3 w-16" />
          </div>
          <div className="search-results-scroll">
            <SearchResultsSkeleton />
          </div>
        </section>
        <section className="search-preview-pane">
          <div className="search-pane-toolbar">
            <Skeleton className="h-5 w-16" />
            <Skeleton className="h-8 w-24" />
          </div>
          <div className="search-preview-scroll">
            <SearchPreviewSkeleton />
          </div>
        </section>
      </div>
    </div>
  );
}
