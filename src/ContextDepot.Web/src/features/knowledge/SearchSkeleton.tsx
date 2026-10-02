import { Field, FieldGroup } from "@/components/ui/field";
import { Skeleton } from "@/components/ui/skeleton";
import { PageHeaderSkeleton } from "@/components/content/PageHeader";
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
      <PageHeaderSkeleton />
      <div className="search-box">
        <FieldGroup>
          <Field>
            <Skeleton className="h-5 w-20" />
            <div className="search-input-group search-input-skeleton">
              <Skeleton className="size-5 shrink-0" />
              <Skeleton className="h-4 w-1/3" />
              <Skeleton className="search-submit-skeleton" />
            </div>
          </Field>
        </FieldGroup>
      </div>
      <FieldGroup className="search-filters">
        <Field className="search-type-field">
          <div className="search-types-skeleton">
            {Array.from({ length: 3 }, (_, index) => (
              <Skeleton className="search-type-skeleton" key={index} />
            ))}
          </div>
        </Field>
        <Field className="search-kind-field">
          <Skeleton className="search-filter-skeleton" />
        </Field>
        <Field className="search-workspace-field">
          <Skeleton className="search-filter-skeleton" />
        </Field>
        <Skeleton className="search-reset-skeleton" />
      </FieldGroup>
      <div className="search-panes">
        <section className="search-results-pane">
          <div className="search-pane-toolbar">
            <Skeleton className="h-6 w-24" />
            <Skeleton className="h-3 w-20" />
          </div>
          <div className="search-results-scroll">
            <SearchResultsSkeleton />
          </div>
          <div className="search-batch-note">
            <Skeleton className="h-4 w-4/5" />
          </div>
        </section>
        <section className="search-preview-pane">
          <div className="search-pane-toolbar">
            <Skeleton className="h-6 w-24" />
            <Skeleton className="h-8 w-24" />
          </div>
          <div className="search-preview-scroll">
            <SearchPreviewSkeleton />
          </div>
        </section>
      </div>
      <div className="search-keyboard-hints">
        <Skeleton className="h-5 w-24" />
        <Skeleton className="h-5 w-20" />
        <Skeleton className="h-5 w-28" />
      </div>
    </div>
  );
}
