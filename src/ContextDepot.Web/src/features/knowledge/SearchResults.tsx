import type { RefObject } from "react";
import { useTranslation } from "react-i18next";
import { ChevronRightIcon, FileTextIcon, LayersIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { RequestFeedback } from "@/components/feedback/RequestFeedback";
import type { Resource } from "@/hooks/use-request-resource";
import { hitKey, type SearchHit, type SearchResponse } from "./search-api";
import { SearchEmpty, SearchResultText } from "./SearchContent";
import { SearchResultsSkeleton } from "./SearchSkeleton";

export function SearchResults({
  resource,
  hits,
  query,
  selected,
  onSelect,
  onRetry,
  onReset,
  scrollRef,
  scopeMissing,
  onClearScope,
}: {
  resource: Resource<SearchResponse>;
  hits: SearchHit[];
  query: string;
  selected: string;
  onSelect: (hit: SearchHit, open: boolean) => void;
  onRetry: () => void;
  onReset?: () => void;
  scopeMissing: boolean;
  onClearScope: () => void;
  scrollRef: RefObject<HTMLDivElement | null>;
}) {
  const { t } = useTranslation();
  function move(index: number, direction: string) {
    const next =
      direction === "Home"
        ? 0
        : direction === "End"
          ? hits.length - 1
          : Math.max(
              0,
              Math.min(
                hits.length - 1,
                index + (direction === "ArrowDown" ? 1 : -1),
              ),
            );
    onSelect(hits[next]!, false);
    scrollRef.current
      ?.querySelectorAll<HTMLButtonElement>(".search-result-row")
      [next]?.focus();
  }
  return (
    <section
      className="search-results-pane"
      aria-labelledby="search-results-title"
      aria-busy={resource.pending}
    >
      <div className="search-pane-toolbar">
        <h2 id="search-results-title">{t("dialogResults")}</h2>
        <span role="status" aria-live="polite">
          {resource.pending
            ? t("loading")
            : resource.data
              ? t("searchDisplayed", { count: hits.length })
              : ""}
        </span>
      </div>
      <div className="search-results-scroll" ref={scrollRef}>
        {resource.error && (
          <RequestFeedback
            title={t(scopeMissing ? "searchScopeMissing" : "dialogSearchError")}
            failure={scopeMissing ? undefined : resource.error}
            description={t(
              scopeMissing ? "searchScopeMissingWhy" : "dialogSearchErrorWhy",
            )}
            pending={resource.pending}
            stale={!!resource.data}
            onRetry={scopeMissing ? undefined : onRetry}
          />
        )}
        {scopeMissing && (
          <Button variant="outline" size="sm" onClick={onClearScope}>
            {t("searchAllSpacesAction")}
          </Button>
        )}
        {!query.trim() ? (
          <SearchEmpty
            title={t("dialogStart")}
            description={t("dialogStartWhy")}
          />
        ) : resource.pending && !resource.data ? (
          <SearchResultsSkeleton />
        ) : (
          (!resource.error || resource.data) &&
          (hits.length ? (
            <ul className="search-result-rows">
              {hits.map((hit, index) => (
                <li key={hitKey(hit)}>
                  <button
                    type="button"
                    className="search-result-row"
                    data-selected={hitKey(hit) === selected || undefined}
                    aria-current={hitKey(hit) === selected ? "true" : undefined}
                    tabIndex={hitKey(hit) === selected ? 0 : -1}
                    onClick={() => onSelect(hit, true)}
                    onFocus={() => {
                      if (hitKey(hit) !== selected) onSelect(hit, false);
                    }}
                    onKeyDown={(event) => {
                      if (
                        ["ArrowUp", "ArrowDown", "Home", "End"].includes(
                          event.key,
                        )
                      ) {
                        event.preventDefault();
                        move(index, event.key);
                      }
                    }}
                  >
                    <span className="search-result-icon">
                      {hit.type === "context" ? (
                        <LayersIcon aria-hidden="true" />
                      ) : (
                        <FileTextIcon aria-hidden="true" />
                      )}
                    </span>
                    <SearchResultText hit={hit} query={query} />
                    <ChevronRightIcon
                      className="search-row-chevron"
                      aria-hidden="true"
                    />
                  </button>
                </li>
              ))}
            </ul>
          ) : (
            <SearchEmpty
              title={t("dialogNoResults")}
              description={t("dialogNoResultsWhy")}
              onReset={onReset}
            />
          ))
        )}
      </div>
      {resource.data && (
        <p className="search-batch-note">
          {t("searchBatchLimit", { count: resource.data.limit })}
        </p>
      )}
    </section>
  );
}
