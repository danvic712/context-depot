import { KnowledgeTypeIcon } from "./KnowledgeTypeIcon";
import type { RefObject } from "react";
import { useTranslation } from "react-i18next";
import { ChevronRightIcon, InfoIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  Popover,
  PopoverContent,
  PopoverTitle,
  PopoverDescription,
  PopoverTrigger,
} from "@/components/ui/popover";
import { RequestFeedback } from "@/components/feedback/RequestFeedback";
import type { Resource } from "@/hooks/use-request-resource";
import {
  type SearchHit,
  type SearchResponse,
  type SearchWorkspace,
} from "./search-api";
import { knowledgeKey } from "@/features/knowledge/knowledge-models";
import { SearchEmpty, SearchResultText } from "./SearchContent";
import { SearchResultsSkeleton } from "./SearchSkeleton";

export function SearchResults({
  resource,
  recent = false,
  degraded,
  spaces,
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
  recent?: boolean;
  degraded?: boolean;
  spaces?: SearchWorkspace[];
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
  const names = new Map(
    (spaces ?? []).map((space) => [space.path, space.name]),
  );
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
      data-recent={recent || undefined}
      aria-labelledby="search-results-title"
      aria-busy={resource.pending}
    >
      <div className="search-pane-toolbar">
        <h2 id="search-results-title">
          {t(recent ? "searchRecent" : "dialogResults")}
        </h2>
        <div className="search-results-status">
          <span role="status" aria-live="polite">
            {resource.pending
              ? t("loading")
              : resource.data
                ? t("searchDisplayed", { count: hits.length })
                : ""}
          </span>
          {degraded && (
            <Popover>
              <PopoverTrigger asChild>
                <Button
                  variant="ghost"
                  size="xs"
                  aria-label={t("dialogDegradedTitle")}
                >
                  <InfoIcon data-icon="inline-start" aria-hidden="true" />
                  {t("searchKeywordMode")}
                </Button>
              </PopoverTrigger>
              <PopoverContent align="end">
                <PopoverTitle>{t("dialogDegradedTitle")}</PopoverTitle>
                <PopoverDescription>{t("dialogDegraded")}</PopoverDescription>
              </PopoverContent>
            </Popover>
          )}
        </div>
      </div>
      <div className="search-results-scroll" ref={scrollRef}>
        {resource.error && (
          <RequestFeedback
            title={t(
              scopeMissing
                ? "searchScopeMissing"
                : recent
                  ? "searchRecentError"
                  : "dialogSearchError",
            )}
            failure={scopeMissing ? undefined : resource.error}
            description={t(
              scopeMissing
                ? "searchScopeMissingWhy"
                : recent
                  ? "searchRecentErrorWhy"
                  : "dialogSearchErrorWhy",
            )}
            pending={resource.pending}
            stale={!!resource.data}
            onRetry={scopeMissing ? undefined : onRetry}
            recoveryAction={
              scopeMissing
                ? { label: t("searchAllSpacesAction"), onClick: onClearScope }
                : resource.error?.kind === "invalidQuery" && onReset
                  ? { label: t("searchReset"), onClick: onReset }
                  : undefined
            }
          />
        )}
        {!query.trim() && !recent ? (
          <SearchEmpty
            title={t("dialogStart")}
            description={t("dialogStartWhy")}
          />
        ) : resource.pending && !resource.data && !resource.error ? (
          <SearchResultsSkeleton />
        ) : (
          (!resource.error || resource.data) &&
          (hits.length ? (
            <ul className="search-result-rows">
              {hits.map((hit, index) => (
                <li key={knowledgeKey(hit)}>
                  <Button
                    type="button"
                    variant="ghost"
                    className="search-result-row"
                    data-selected={knowledgeKey(hit) === selected || undefined}
                    aria-current={
                      knowledgeKey(hit) === selected ? "true" : undefined
                    }
                    tabIndex={knowledgeKey(hit) === selected ? 0 : -1}
                    onClick={() => onSelect(hit, true)}
                    onFocus={() => {
                      if (knowledgeKey(hit) !== selected) onSelect(hit, false);
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
                      <KnowledgeTypeIcon type={hit.type} />
                    </span>
                    <SearchResultText
                      hit={hit}
                      query={query}
                      plainMetadata
                      workspaceName={
                        names.get(hit.workspace) ?? t("searchUnknownSpace")
                      }
                    />
                    <ChevronRightIcon
                      className="search-row-chevron"
                      aria-hidden="true"
                    />
                  </Button>
                </li>
              ))}
            </ul>
          ) : (
            <SearchEmpty
              title={t(
                recent
                  ? onReset
                    ? "searchRecentFilteredEmpty"
                    : "searchRecentEmpty"
                  : "dialogNoResults",
              )}
              description={t(
                recent
                  ? onReset
                    ? "searchRecentFilteredEmptyWhy"
                    : "searchRecentEmptyWhy"
                  : "dialogNoResultsWhy",
              )}
              onReset={onReset}
            />
          ))
        )}
      </div>
      {resource.data && (
        <p className="search-batch-note">
          {t(recent ? "searchRecentLimit" : "searchBatchLimit", {
            count: resource.data.limit,
          })}
        </p>
      )}
    </section>
  );
}
