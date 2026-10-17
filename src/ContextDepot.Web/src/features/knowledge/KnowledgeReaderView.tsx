import { KnowledgeCopyButton } from "./KnowledgeCopyButton";
import type { ReactNode } from "react";
import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import { ArrowLeftIcon, XIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { RequestFeedback } from "@/components/feedback/RequestFeedback";
import type { Resource } from "@/hooks/use-request-resource";
import { KnowledgeContent, KnowledgeMetadata } from "./KnowledgeContent";
import {
  KnowledgeContentSkeleton,
  KnowledgeReaderTitleSkeleton,
  KnowledgeReaderMetadataSkeleton,
} from "./KnowledgeReaderSkeleton";
import { knowledgeReturnTarget } from "./knowledge-navigation";
import { DeleteKnowledgeDialog } from "./DeleteKnowledgeDialog";
import {
  SheetClose,
  SheetHeader,
  SheetTitle,
  SheetFooter,
} from "@/components/ui/sheet";
import { Separator } from "@/components/ui/separator";
import type { KnowledgePreview, KnowledgeType } from "./knowledge-models";
import "@/styles/knowledge-reader.css";

export function KnowledgeReaderView({
  resource,
  source,
  onRetry,
  spaceId,
  search,
  onDeleted,
  panel = false,
  type = "document",
  fragment,
  navigation,
  contentKey = "",
  titleHint,
}: {
  resource: Resource<KnowledgePreview>;
  source?: unknown;
  onRetry: () => void;
  spaceId?: string;
  search?: string;
  onDeleted?: () => void;
  panel?: boolean;
  type?: KnowledgeType;
  fragment?: string;
  navigation?: ReactNode;
  contentKey?: string;
  titleHint?: string;
}) {
  const { t } = useTranslation();
  const back = knowledgeReturnTarget(source, spaceId, search);
  const loading = !resource.data && !resource.error;
  const panelTitle = resource.error
    ? undefined
    : (resource.data?.title ?? titleHint);
  const readerLabel = t(
    type === "context" ? "contextDetailTitle" : "documentReaderTitle",
  );
  const errorTitle =
    resource.error?.kind === "notFound"
      ? "knowledgeNotFound"
      : resource.error?.kind === "forbidden"
        ? "knowledgeForbidden"
        : "knowledgeLoadError";
  const actions = (
    <>
      <KnowledgeCopyButton
        detail={resource.error ? undefined : resource.data}
        pending={resource.pending}
        iconOnly={!panel}
      />
      {!resource.error && resource.data && onDeleted ? (
        <DeleteKnowledgeDialog
          item={resource.data}
          disabled={resource.pending}
          onDeleted={onDeleted}
          onRefresh={onRetry}
          iconOnly={!panel}
        />
      ) : panel && onDeleted ? (
        <span className="reader-delete-placeholder" aria-hidden="true" />
      ) : null}
    </>
  );
  const body = resource.error ? (
    <div className="knowledge-reader-error">
      <RequestFeedback
        title={t(errorTitle)}
        headingLevel={panel ? 2 : 1}
        failure={resource.error}
        pending={resource.pending}
        onRetry={onRetry}
      />
    </div>
  ) : resource.data ? (
    <KnowledgeContent
      detail={resource.data}
      fragment={fragment}
      headingLevel={1}
      showWorkspace={!spaceId}
      showHeading={!panel}
    />
  ) : (
    <div role="status" aria-label={t("loading")}>
      <KnowledgeContentSkeleton showHeading={!panel} />
    </div>
  );
  if (panel)
    return (
      <section
        className="knowledge-reader"
        data-panel
        aria-busy={resource.pending}
      >
        <SheetHeader className="knowledge-reader-header">
          <div className="knowledge-reader-header-navigation">
            {navigation}
            <SheetClose asChild>
              <Button
                variant="ghost"
                size="icon"
                className="knowledge-reader-close"
                aria-label={t("spacesCloseReader")}
                title={t("spacesCloseReader")}
              >
                <XIcon aria-hidden="true" />
              </Button>
            </SheetClose>
          </div>
          <SheetTitle asChild>
            <h1 tabIndex={-1} title={panelTitle}>
              {panelTitle ??
                (loading ? (
                  <>
                    <span className="sr-only">{readerLabel}</span>
                    <KnowledgeReaderTitleSkeleton />
                  </>
                ) : (
                  readerLabel
                ))}
            </h1>
          </SheetTitle>
          {!resource.error && resource.data ? (
            <KnowledgeMetadata
              detail={resource.data}
              showWorkspace={false}
              showType
            />
          ) : loading ? (
            <KnowledgeReaderMetadataSkeleton />
          ) : (
            <div className="reader-metadata-skeleton" aria-hidden="true" />
          )}
        </SheetHeader>
        <Separator />
        <article
          className="knowledge-reader-body"
          tabIndex={0}
          aria-label={t(
            type === "context" ? "contextDetailTitle" : "documentReaderTitle",
          )}
        >
          <div
            key={
              resource.data
                ? `${contentKey}:${resource.data.type}:${resource.data.id}`
                : resource.error
                  ? "error"
                  : "pending"
            }
            className="knowledge-reader-content"
            data-pending={resource.pending || undefined}
          >
            {body}
          </div>
        </article>
        <Separator />
        <SheetFooter className="knowledge-reader-footer">{actions}</SheetFooter>
      </section>
    );
  return (
    <section className="knowledge-reader" aria-busy={resource.pending}>
      <div className="knowledge-reader-toolbar">
        <Button asChild variant="ghost">
          <Link
            to={back.to}
            preventScrollReset
            state={
              source && typeof source === "object" && "focusKnowledge" in source
                ? { focusKnowledge: source.focusKnowledge }
                : undefined
            }
          >
            <ArrowLeftIcon data-icon="inline-start" aria-hidden="true" />
            {t(back.label)}
          </Link>
        </Button>
        {navigation}
        <div className="knowledge-reader-actions">{actions}</div>
      </div>
      <article className="knowledge-reader-document">
        <div
          key={
            resource.data
              ? `${contentKey}:${resource.data.type}:${resource.data.id}`
              : resource.error
                ? "error"
                : "pending"
          }
          className="knowledge-reader-content"
          data-pending={resource.pending || undefined}
        >
          {body}
        </div>
      </article>
    </section>
  );
}
