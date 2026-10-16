import { useDisplayPreference } from "@/lib/display-preferences";
import { stripDisplayParams } from "@/lib/display-query";
import { useCallback, useEffect, useRef, useState } from "react";
import { useLocation, useSearchParams } from "react-router";
import { useTranslation } from "react-i18next";
import { FileTextIcon } from "lucide-react";
import { getSpaceKnowledge } from "./spaces-api";
import { useRequestResource } from "@/hooks/use-request-resource";
import {
  SpaceKnowledgeList,
  SpaceKnowledgeSkeleton,
} from "./SpaceKnowledgeList";
import { knowledgePath } from "@/features/knowledge/knowledge-routes";
import { knowledgeKey } from "@/features/knowledge/knowledge-models";
import { RequestFeedback } from "@/components/feedback/RequestFeedback";
import { PaginationBar } from "@/components/content/PaginationBar";
import { paginationDefaults } from "@/lib/pagination";
import { Button } from "@/components/ui/button";
import {
  Empty,
  EmptyHeader,
  EmptyMedia,
  EmptyTitle,
  EmptyDescription,
} from "@/components/ui/empty";

export function SpaceKnowledge({ spaceId }: { spaceId: string }) {
  const { t } = useTranslation();
  const location = useLocation();
  const [params, setParams] = useSearchParams();
  const [layout] = useDisplayPreference("knowledgeLayout");
  const requested = Number(params.get("knowledgePage") ?? 1);
  const page =
    Number.isSafeInteger(requested) &&
    requested > 0 &&
    requested <= paginationDefaults.maximumPage
      ? requested
      : 1;
  const [pageSize, setPageSize] = useDisplayPreference("knowledgePageSize");
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
  const list = useRef<HTMLDivElement>(null);
  useEffect(() => {
    const key = location.state?.focusKnowledge;
    if (
      !data ||
      typeof key !== "string" ||
      /\/(contexts|documents)\/[^/]+$/.test(location.pathname)
    )
      return;
    Array.from(
      list.current?.querySelectorAll<HTMLAnchorElement>(
        ".space-knowledge-row",
      ) ?? [],
    )
      .find((row) => row.dataset.key === key)
      ?.focus();
  }, [data, location.state, location.pathname]);
  function changePage(value: number) {
    const next = stripDisplayParams(params);
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
    const next = stripDisplayParams(params);
    next.delete("selected");
    next.delete("knowledgePage");
    setPageSize(value);
    setParams(next, { preventScrollReset: true });
  }
  return (
    <section
      className="space-knowledge-content"
      aria-label={t("spacesKnowledgeTitle")}
      aria-busy={resource.pending}
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
      <div className="space-knowledge-workbench">
        <div ref={list} className="space-knowledge-list-pane">
          <h2 className="sr-only">{t("spacesKnowledgeTitle")}</h2>
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
                getHref={(value) =>
                  knowledgePath(value.type, value.id, spaceId) + location.search
                }
                getState={(value) => ({
                  from: location.pathname + location.search,
                  focusKnowledge: knowledgeKey(value),
                  navigation: "spaces",
                })}
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
      </div>
    </section>
  );
}
