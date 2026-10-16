import { paginationDefaults } from "@/lib/pagination";
import { useDisplayPreference } from "@/lib/display-preferences";
import { stripDisplayParams } from "@/lib/display-query";
import { useCallback, useEffect, useRef, useState } from "react";
import { Link, useLocation, useSearchParams } from "react-router";
import { useTranslation } from "react-i18next";
import { ArchiveIcon, FileTextIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  Empty,
  EmptyHeader,
  EmptyMedia,
  EmptyTitle,
  EmptyDescription,
} from "@/components/ui/empty";
import { RequestFeedback } from "@/components/feedback/RequestFeedback";
import { PaginationBar } from "@/components/content/PaginationBar";
import { useRequestResource } from "@/hooks/use-request-resource";
import { getArchivedDocuments } from "@/features/knowledge/document-archive-api";
import { SpaceKnowledgeSkeleton } from "./SpaceKnowledgeList";
import { knowledgePath } from "@/features/knowledge/knowledge-routes";
import { DeleteKnowledgeDialog } from "@/features/knowledge/DeleteKnowledgeDialog";

export function ArchivedDocuments({ spaceId }: { spaceId: string }) {
  const { t, i18n } = useTranslation();
  const [params, setParams] = useSearchParams();
  const requested = Number(params.get("archivePage") ?? 1);
  const page =
    Number.isSafeInteger(requested) &&
    requested >= 1 &&
    requested <= paginationDefaults.maximumPage
      ? requested
      : 1;
  const [pageSize, setPageSize] = useDisplayPreference("archivePageSize");
  const [attempt, setAttempt] = useState(0);
  const [deletedIds, setDeletedIds] = useState<ReadonlySet<string>>(new Set());
  const focusAfterDelete = useRef(false);
  const load = useCallback(
    (signal: AbortSignal) =>
      getArchivedDocuments(spaceId, page, pageSize, signal),
    [spaceId, page, pageSize],
  );
  const resource = useRequestResource(
    `${spaceId}:${page}:${pageSize}`,
    attempt,
    load,
  );
  const data =
    resource.error && !resource.error.retryable ? undefined : resource.data;
  const items = data?.items.filter((item) => !deletedIds.has(item.id)) ?? [];
  const totalCount = data
    ? data.totalCount -
      data.items.filter((item) => deletedIds.has(item.id)).length
    : undefined;
  const location = useLocation();
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
        ".archived-document-open",
      ) ?? [],
    )
      .find((row) => row.dataset.id === key)
      ?.focus();
  }, [data, location.state, location.pathname]);
  const date = new Intl.DateTimeFormat(i18n.resolvedLanguage, {
    dateStyle: "medium",
  });
  useEffect(() => {
    if (resource.pending || !data || !focusAfterDelete.current) return;
    focusAfterDelete.current = false;
    if (document.activeElement === document.body)
      list.current
        ?.querySelector<HTMLHeadingElement>("h2")
        ?.focus({ preventScroll: true });
  }, [data, resource.pending]);
  function paginate(value: number, size = pageSize) {
    const next = stripDisplayParams(params);
    next.delete("archivedSelected");
    if (value === 1) next.delete("archivePage");
    else next.set("archivePage", String(value));
    if (size !== pageSize) setPageSize(size);
    setParams(next, { preventScrollReset: true });
  }
  function deleted(id: string) {
    focusAfterDelete.current = true;
    setDeletedIds((current) => new Set([...current, id]));
    const next = stripDisplayParams(params);
    if (next.get("archivedSelected") === id) next.delete("archivedSelected");
    if (items.length === 1 && page > 1)
      next.set("archivePage", String(page - 1));
    setParams(next, { replace: true, preventScrollReset: true });
    setAttempt((value) => value + 1);
  }
  return (
    <section
      className="space-knowledge-content archived-documents"
      aria-label={t("spacesArchivedDocuments")}
      aria-busy={resource.pending}
    >
      {resource.error && (
        <RequestFeedback
          title={t("spacesArchiveLoadError")}
          failure={resource.error}
          pending={resource.pending}
          stale={!!data}
          onRetry={() => setAttempt((value) => value + 1)}
        />
      )}
      <div className="space-knowledge-workbench">
        <div ref={list} className="space-knowledge-list-pane">
          <h2 className="sr-only" tabIndex={-1}>
            {t("spacesArchivedDocuments")}
          </h2>
          {(totalCount ?? 0) > 0 && (
            <p className="space-archive-note">{t("spacesArchiveHint")}</p>
          )}
          {!data && resource.pending && !resource.error && (
            <>
              <span className="sr-only" role="status">
                {t("loading")}
              </span>
              <SpaceKnowledgeSkeleton />
            </>
          )}
          {!!items.length && (
            <ul className="archived-document-list">
              {items.map((document) => (
                <li key={document.id}>
                  <Button
                    variant="ghost"
                    className="archived-document-open"
                    data-id={document.id}
                    asChild
                  >
                    <Link
                      preventScrollReset
                      to={
                        knowledgePath("document", document.id, spaceId) +
                        location.search
                      }
                      state={{
                        from: location.pathname + location.search,
                        navigation: "spaces",
                        focusKnowledge: document.id,
                      }}
                    >
                      <FileTextIcon aria-hidden="true" />
                      <span>
                        <strong>{document.title}</strong>
                        <code>{document.path}</code>
                        <time dateTime={document.updatedAt}>
                          {t("spacesArchivedAt", {
                            date: date.format(new Date(document.updatedAt)),
                          })}
                        </time>
                      </span>
                    </Link>
                  </Button>
                  <DeleteKnowledgeDialog
                    item={{ ...document, type: "document" }}
                    onDeleted={() => deleted(document.id)}
                    onRefresh={() => setAttempt((value) => value + 1)}
                  />
                </li>
              ))}
            </ul>
          )}
          {data && !items.length && !resource.error && (
            <Empty className="spaces-empty">
              <EmptyHeader>
                <EmptyMedia variant="icon">
                  <ArchiveIcon aria-hidden="true" />
                </EmptyMedia>
                <EmptyTitle>
                  {t(
                    totalCount
                      ? "spacesArchivePageEmpty"
                      : "spacesArchiveEmpty",
                  )}
                </EmptyTitle>
                <EmptyDescription>
                  {t(
                    totalCount
                      ? "spacesPageEmptyDescription"
                      : "spacesArchiveEmptyWhy",
                  )}
                </EmptyDescription>
              </EmptyHeader>
              {(totalCount ?? 0) > 0 && (
                <Button variant="outline" onClick={() => paginate(1)}>
                  {t("spacesFirstPage")}
                </Button>
              )}
            </Empty>
          )}
          {(data || resource.pending) && (
            <PaginationBar
              page={page}
              pageSize={pageSize}
              totalCount={totalCount}
              itemCount={items.length}
              loading={resource.pending && !data}
              ariaLabel={t("spacesArchivePagination")}
              onPageChange={paginate}
              onPageSizeChange={(value) => paginate(1, value)}
            />
          )}
        </div>
      </div>
    </section>
  );
}
