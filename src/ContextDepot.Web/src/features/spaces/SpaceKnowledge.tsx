import { useCallback, useState } from "react";
import { useSearchParams } from "react-router";
import { useTranslation } from "react-i18next";
import { ChevronLeftIcon, ChevronRightIcon, FileTextIcon } from "lucide-react";
import { getSpaceKnowledge } from "./spaces-api";
import { useRequestResource } from "@/hooks/use-request-resource";
import { RecentKnowledge } from "@/features/home/HomeCollections";
import { RequestFeedback } from "@/components/feedback/RequestFeedback";
import { Button } from "@/components/ui/button";
import {
  Empty,
  EmptyHeader,
  EmptyMedia,
  EmptyTitle,
  EmptyDescription,
} from "@/components/ui/empty";
import "@/styles/home.css";

export function SpaceKnowledge({ spaceId }: { spaceId: string }) {
  const { t } = useTranslation();
  const [params, setParams] = useSearchParams();
  const requested = Number(params.get("knowledgePage") ?? 1);
  const page =
    Number.isSafeInteger(requested) && requested > 0 && requested <= 100000
      ? requested
      : 1;
  const [attempt, setAttempt] = useState(0);
  const load = useCallback(
    (signal: AbortSignal) => getSpaceKnowledge(spaceId, page, signal),
    [spaceId, page],
  );
  const resource = useRequestResource(`${spaceId}:${page}`, attempt, load);
  const data =
    resource.error && !resource.error.retryable ? undefined : resource.data;
  const pages = Math.max(1, Math.ceil((data?.totalCount ?? 0) / 12));
  function changePage(value: number) {
    const next = new URLSearchParams(params);
    if (value === 1) next.delete("knowledgePage");
    else next.set("knowledgePage", String(value));
    setParams(next, { preventScrollReset: true });
  }
  return (
    <div className="space-knowledge-content" aria-busy={resource.pending}>
      {resource.error && (
        <RequestFeedback
          title={t("spacesKnowledgeError")}
          failure={resource.error}
          onRetry={() => setAttempt((value) => value + 1)}
          pending={resource.pending}
          stale={!!data}
        />
      )}
      <RecentKnowledge
        items={data?.items ?? []}
        pending={!data && resource.pending}
        navigation="spaces"
      />
      {data && !data.items.length && !resource.error && (
        <Empty className="spaces-empty">
          <EmptyHeader>
            <EmptyMedia className="spaces-empty-art">
              <FileTextIcon aria-hidden="true" />
            </EmptyMedia>
            <EmptyTitle>
              {t(data.totalCount ? "spacesPageEmpty" : "spacesKnowledgeEmpty")}
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
      {data && pages > 1 && (
        <nav
          className="spaces-pagination"
          aria-label={t("spacesKnowledgePagination")}
        >
          <span role="status">{t("spacesPage", { page, pages })}</span>
          <div>
            <Button
              variant="outline"
              size="sm"
              disabled={page <= 1 || resource.pending}
              onClick={() => changePage(page - 1)}
            >
              <ChevronLeftIcon aria-hidden="true" />
              {t("spacesPrevious")}
            </Button>
            <Button
              variant="outline"
              size="sm"
              disabled={page >= pages || resource.pending}
              onClick={() => changePage(page + 1)}
            >
              {t("spacesNext")}
              <ChevronRightIcon aria-hidden="true" />
            </Button>
          </div>
        </nav>
      )}
    </div>
  );
}
