import { useTranslation } from "react-i18next";
import { SearchBox } from "@/features/knowledge/SearchBox";
import { SearchFilters } from "@/features/knowledge/SearchFilters";
import { SearchResults } from "@/features/knowledge/SearchResults";
import { SearchPreview } from "@/features/knowledge/SearchPreview";
import { useSearchPage } from "@/features/knowledge/use-search-page";
import "@/styles/search.css";

export function Search() {
  const { t } = useTranslation();
  const page = useSearchPage();
  return (
    <div
      className="search-workbench"
      data-reading={page.reading || undefined}
      data-list-only={
        (!page.hits.length && !page.resource.pending) || undefined
      }
      onKeyDown={(event) => {
        if (event.nativeEvent.isComposing || event.keyCode === 229) return;
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
      <h1 className="sr-only">{t("searchTitle")}</h1>
      <div className="search-panes">
        <SearchFilters
          params={page.params}
          spaces={page.spaces.data}
          pending={page.spaces.pending}
          error={page.spaces.error}
          onRetry={page.retrySpaces}
          onFilter={page.filter}
          onReset={page.reset}
        >
          <SearchBox
            sidebar
            value={page.query}
            onChange={(value) => page.changeQuery(value)}
            onSearch={(value) => page.changeQuery(value, true)}
          />
        </SearchFilters>
        <SearchResults
          recent={page.recent}
          resource={page.resource}
          degraded={page.degraded}
          spaces={
            page.spaces.error?.retryable === false
              ? undefined
              : page.spaces.data
          }
          hits={page.hits}
          query={page.query}
          selected={
            page.selected ? `${page.selected.type}:${page.selected.id}` : ""
          }
          onSelect={page.select}
          onRetry={page.retrySearch}
          onReset={page.activeFilterCount ? page.reset : undefined}
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
          workspace={(page.spaces.error?.retryable === false
            ? undefined
            : page.spaces.data
          )?.find((space) => space.path === page.detail.data?.workspace)}
          searchPending={page.resource.pending}
          onBack={page.back}
          onRetry={page.retryPreview}
          paneRef={page.paneRef}
        />
      </div>
    </div>
  );
}
