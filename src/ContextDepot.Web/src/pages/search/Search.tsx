import { useTranslation } from "react-i18next";
import { RequestFeedback } from "@/components/feedback/RequestFeedback";
import { SearchBox } from "@/features/knowledge/SearchBox";
import { SearchFilters } from "@/features/knowledge/SearchFilters";
import { SearchResults } from "@/features/knowledge/SearchResults";
import { SearchPreview } from "@/features/knowledge/SearchPreview";
import { useSearchPage } from "@/features/knowledge/use-search-page";
import { PageHeader } from "@/components/content/PageHeader";
import stillLife from "@/assets/home-still-life.png";
import "@/styles/search.css";

export function Search() {
  const { t } = useTranslation();
  const page = useSearchPage();
  return (
    <div
      className="search-workbench"
      data-reading={page.reading || undefined}
      onKeyDown={(event) => {
        if (
          event.key === "Escape" &&
          page.paneRef.current?.contains(event.target as Node)
        ) {
          event.preventDefault();
          page.back();
        } else if (
          event.key === "ArrowDown" &&
          (event.target as HTMLElement).id === "knowledge-search" &&
          page.hits.length
        ) {
          event.preventDefault();
          page.listRef.current
            ?.querySelector<HTMLButtonElement>("[data-selected]")
            ?.focus();
        }
      }}
    >
      <PageHeader
        eyebrow={t("searchKicker")}
        title={t("searchTitle")}
        description={t("searchSub")}
        art={stillLife}
      />
      <SearchBox
        value={page.query}
        onChange={(value) => page.changeQuery(value)}
        onSearch={(value) => page.changeQuery(value, true)}
      />
      <SearchFilters
        params={page.params}
        hits={page.resource.data?.items}
        spaces={page.spaces.data}
        pending={page.spaces.pending}
        onFilter={page.filter}
        onReset={page.reset}
      />
      {page.spaces.error && (
        <RequestFeedback
          title={t("dialogWorkspaceError")}
          failure={page.spaces.error}
          description={t("dialogWorkspaceErrorWhy")}
          pending={page.spaces.pending}
          onRetry={page.retrySpaces}
          compact
        />
      )}
      {page.degraded && (
        <RequestFeedback
          tone="warning"
          title={t("dialogDegradedTitle")}
          description={t("dialogDegraded")}
          compact
        />
      )}
      <div className="search-panes">
        <SearchResults
          resource={page.resource}
          hits={page.hits}
          query={page.query}
          selected={
            page.selected ? `${page.selected.type}:${page.selected.id}` : ""
          }
          onSelect={page.select}
          onRetry={page.retrySearch}
          onReset={
            page.params.has("type") ||
            page.params.has("kind") ||
            page.params.has("workspace")
              ? page.reset
              : undefined
          }
          scopeMissing={
            page.params.has("workspace") &&
            page.resource.error?.kind === "notFound"
          }
          onClearScope={() => page.filter("workspace", "all")}
          scrollRef={page.listRef}
        />
        <SearchPreview
          item={page.selected}
          resource={page.detail}
          searchPending={page.resource.pending && !!page.query.trim()}
          onBack={page.back}
          onRetry={page.retryPreview}
          paneRef={page.paneRef}
        />
      </div>
      <div className="search-keyboard-hints" aria-hidden="true">
        <span>
          <kbd>↑</kbd>
          <kbd>↓</kbd>
          {t("dialogNavigate")}
        </span>
        <span>
          <kbd>Enter</kbd>
          {t("dialogPreview")}
        </span>
        <span>
          <kbd>Esc</kbd>
          {t("dialogBack")}
        </span>
      </div>
    </div>
  );
}
