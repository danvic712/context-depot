import { useCallback, useEffect, useRef, useState } from "react";
import { useSearchParams } from "react-router";
import { useTranslation } from "react-i18next";
import { FileTextIcon, LayoutGridIcon, ListIcon } from "lucide-react";
import { getSpaceKnowledge, selectedSpaceKnowledge } from "./spaces-api";
import { useRequestResource } from "@/hooks/use-request-resource";
import {
  SpaceKnowledgeList,
  SpaceKnowledgeSkeleton,
} from "./SpaceKnowledgeList";
import { SpaceReader } from "./SpaceReader";
import { hitKey } from "@/features/knowledge/search-api";
import { RequestFeedback } from "@/components/feedback/RequestFeedback";
import { PaginationBar } from "@/components/content/PaginationBar";
import { paginationDefaults, readPageSize } from "@/lib/pagination";
import { Button } from "@/components/ui/button";
import { ToggleGroup, ToggleGroupItem } from "@/components/ui/toggle-group";
import {
  Empty,
  EmptyHeader,
  EmptyMedia,
  EmptyTitle,
  EmptyDescription,
} from "@/components/ui/empty";

export function SpaceKnowledge({ spaceId }: { spaceId: string }) {
  const { t, i18n } = useTranslation();
  const [params, setParams] = useSearchParams();
  const layout = params.get("knowledgeLayout") === "cards" ? "cards" : "list";
  const requested = Number(params.get("knowledgePage") ?? 1);
  const page =
    Number.isSafeInteger(requested) && requested > 0 && requested <= 100000
      ? requested
      : 1;
  const pageSize = readPageSize(params.get("knowledgePageSize"));
  const [attempt, setAttempt] = useState(0);
  const load = useCallback(
    (signal: AbortSignal) => getSpaceKnowledge(spaceId, page, signal, pageSize),
    [spaceId, page, pageSize],
  );
  const resource = useRequestResource(
    `${spaceId}:${page}:${pageSize}`,
    attempt,
    load,
  );
  const data =
    resource.error && !resource.error.retryable ? undefined : resource.data;
  const selected = params.get("selected");
  const item = selectedSpaceKnowledge(data?.items ?? [], selected, spaceId);
  const list = useRef<HTMLDivElement>(null);
  const restoreFocus = useRef<string | null>(null);
  useEffect(() => {
    if (item || !restoreFocus.current) return;
    const key = restoreFocus.current;
    restoreFocus.current = null;
    const row = Array.from(
      list.current?.querySelectorAll<HTMLButtonElement>(
        ".space-knowledge-row",
      ) ?? [],
    ).find((button) => button.dataset.key === key);
    row?.focus();
  }, [item]);
  function closeReader() {
    restoreFocus.current = selected;
    const next = new URLSearchParams(params);
    next.delete("selected");
    setParams(next, { preventScrollReset: true });
  }
  function changePage(value: number) {
    const next = new URLSearchParams(params);
    next.delete("selected");
    if (value === 1) next.delete("knowledgePage");
    else next.set("knowledgePage", String(value));
    setParams(next, { preventScrollReset: true });
  }
  function changePageSize(value: number) {
    if (
      !Number.isSafeInteger(value) ||
      value < 1 ||
      value > paginationDefaults.maximumPageSize ||
      value === pageSize
    )
      return;
    const next = new URLSearchParams(params);
    next.delete("selected");
    next.delete("knowledgePage");
    if (value === paginationDefaults.pageSize) next.delete("knowledgePageSize");
    else next.set("knowledgePageSize", String(value));
    setParams(next, { preventScrollReset: true });
  }
  return (
    <section
      className="space-knowledge-content"
      aria-label={t("spacesKnowledgeTitle")}
      aria-busy={resource.pending}
      onKeyDown={(event) => {
        if (event.key === "Escape" && item) {
          event.preventDefault();
          closeReader();
        }
      }}
    >
      {resource.error && (
        <RequestFeedback
          title={t("spacesKnowledgeError")}
          failure={resource.error}
          onRetry={() => setAttempt((value) => value + 1)}
          pending={resource.pending}
          stale={!!data}
          recoveryAction={
            resource.error.kind === "invalidQuery" && page > 1
              ? { label: t("spacesFirstPage"), onClick: () => changePage(1) }
              : undefined
          }
        />
      )}
      {selected && data && !item && !resource.pending && (
        <div className="space-selection-unavailable" role="status">
          <span>{t("spacesSelectionUnavailable")}</span>
          <Button variant="outline" size="sm" onClick={closeReader}>
            {t("spacesReturnList")}
          </Button>
        </div>
      )}
      <div
        className="space-knowledge-workbench"
        data-reading={!!item || undefined}
      >
        <div ref={list} className="space-knowledge-list-pane">
          <div className="spaces-section-heading space-knowledge-toolbar">
            <h2>
              {t("spacesKnowledgeTitle")}
              {data && (
                <span className="spaces-directory-total">
                  {new Intl.NumberFormat(i18n.resolvedLanguage).format(
                    data.totalCount,
                  )}
                </span>
              )}
            </h2>
            <ToggleGroup
              className="space-layout-switch"
              type="single"
              variant="outline"
              value={layout}
              aria-label={t("spacesDisplayMode")}
              onValueChange={(value) => {
                if (!value) return;
                const next = new URLSearchParams(params);
                if (value === "list") next.delete("knowledgeLayout");
                else next.set("knowledgeLayout", value);
                setParams(next, { preventScrollReset: true });
              }}
            >
              <ToggleGroupItem value="list">
                <ListIcon aria-hidden="true" />
                {t("spacesListView")}
              </ToggleGroupItem>
              <ToggleGroupItem value="cards">
                <LayoutGridIcon aria-hidden="true" />
                {t("spacesCardView")}
              </ToggleGroupItem>
            </ToggleGroup>
          </div>
          {!data && resource.pending && !resource.error && (
            <>
              <span className="sr-only" role="status">
                {t("loading")}
              </span>
              <SpaceKnowledgeSkeleton layout={layout} />
            </>
          )}
          {!!data?.items.length && (
            <div className="space-knowledge-panel" data-layout={layout}>
              <SpaceKnowledgeList
                layout={layout}
                items={data.items}
                selected={item ? hitKey(item) : undefined}
                onSelect={(value) => {
                  const next = new URLSearchParams(params);
                  next.set("selected", hitKey(value));
                  setParams(next, { preventScrollReset: true });
                }}
              />
            </div>
          )}
          {data && !data.items.length && !resource.error && (
            <Empty className="spaces-empty">
              <EmptyHeader>
                <EmptyMedia className="spaces-empty-art">
                  <FileTextIcon aria-hidden="true" />
                </EmptyMedia>
                <EmptyTitle>
                  {t(
                    data.totalCount
                      ? "spacesKnowledgePageEmpty"
                      : "spacesKnowledgeEmpty",
                  )}
                </EmptyTitle>
                <EmptyDescription>
                  {t(
                    data.totalCount
                      ? "spacesPageEmptyDescription"
                      : "spacesKnowledgeEmptyWhy",
                  )}
                </EmptyDescription>
              </EmptyHeader>
              {data.totalCount > 0 && (
                <Button variant="outline" onClick={() => changePage(1)}>
                  {t("spacesFirstPage")}
                </Button>
              )}
            </Empty>
          )}
          {(data || resource.pending) && (
            <PaginationBar
              page={page}
              pageSize={pageSize}
              totalCount={data?.totalCount}
              itemCount={data?.items.length ?? 0}
              loading={resource.pending && !data}
              ariaLabel={t("spacesKnowledgePagination")}
              onPageChange={changePage}
              onPageSizeChange={changePageSize}
            />
          )}
        </div>
        {item && (
          <SpaceReader key={hitKey(item)} item={item} onClose={closeReader} />
        )}
      </div>
    </section>
  );
}
