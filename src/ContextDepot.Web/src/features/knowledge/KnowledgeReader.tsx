import { KnowledgeCopyButton } from "./KnowledgeCopyButton";
import { useCallback, useEffect, useRef, useState } from "react";
import { Link, useLocation, useNavigate, useParams } from "react-router";
import { useTranslation } from "react-i18next";
import { ArrowLeftIcon, XIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { RequestFeedback } from "@/components/feedback/RequestFeedback";
import {
  useRequestResource,
  type Resource,
} from "@/hooks/use-request-resource";
import { KnowledgeContent, KnowledgeMetadata } from "./KnowledgeContent";
import { KnowledgeContentSkeleton } from "./KnowledgeReaderSkeleton";
import { knowledgeReturnTarget } from "./knowledge-navigation";
import { DeleteKnowledgeDialog } from "./DeleteKnowledgeDialog";
import {
  Sheet,
  SheetClose,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetFooter,
} from "@/components/ui/sheet";
import { Separator } from "@/components/ui/separator";
import { getKnowledgePreview } from "./search-api";
import {
  type KnowledgePreview,
  type KnowledgeType,
} from "@/features/knowledge/knowledge-models";
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
}: {
  resource: Resource<KnowledgePreview>;
  source?: unknown;
  onRetry: () => void;
  spaceId?: string;
  search?: string;
  onDeleted?: () => void;
  panel?: boolean;
  type?: KnowledgeType;
}) {
  const { t } = useTranslation();
  const back = knowledgeReturnTarget(source, spaceId, search);
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
      {!resource.error && resource.data && onDeleted && (
        <DeleteKnowledgeDialog
          item={resource.data}
          disabled={resource.pending}
          onDeleted={onDeleted}
          onRefresh={onRetry}
          iconOnly={!panel}
        />
      )}
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
          <SheetTitle asChild>
            <h1 tabIndex={-1}>
              {!resource.error && resource.data
                ? resource.data.title
                : t(
                    type === "context"
                      ? "contextDetailTitle"
                      : "documentReaderTitle",
                  )}
            </h1>
          </SheetTitle>
          {!resource.error && resource.data && (
            <KnowledgeMetadata
              detail={resource.data}
              showWorkspace={false}
              showType
            />
          )}
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
        </SheetHeader>
        <Separator />
        <article
          className="knowledge-reader-body"
          tabIndex={0}
          aria-label={t(
            type === "context" ? "contextDetailTitle" : "documentReaderTitle",
          )}
        >
          {body}
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
        <div className="knowledge-reader-actions">{actions}</div>
      </div>
      <article className="knowledge-reader-document">{body}</article>
    </section>
  );
}

export function KnowledgeReader({ type }: { type: KnowledgeType }) {
  const { knowledgeId = "", spaceId } = useParams();
  const { state, search } = useLocation();
  const navigate = useNavigate();
  const { t } = useTranslation();
  const [attempt, setAttempt] = useState(0);
  const isPanel = !!spaceId;
  const [open, setOpen] = useState(true);
  const closing = useRef(false);
  const reader = useRef<HTMLDivElement>(null);
  const load = useCallback(
    (signal: AbortSignal) =>
      getKnowledgePreview({ type, id: knowledgeId }, signal, spaceId),
    [type, knowledgeId, spaceId],
  );
  const resource = useRequestResource(
    `${spaceId ?? "global"}:${type}:${knowledgeId}`,
    attempt,
    load,
  );
  function back(deleted = false) {
    navigate(knowledgeReturnTarget(state, spaceId, search).to, {
      replace: deleted,
      preventScrollReset: true,
      state: deleted
        ? { refreshKnowledge: Date.now() }
        : state && typeof state === "object" && "focusKnowledge" in state
          ? { focusKnowledge: state.focusKnowledge }
          : undefined,
    });
  }
  function closePanel() {
    closing.current = true;
    setOpen(false);
  }
  useEffect(() => {
    document.title = `ContextDepot · ${
      resource.error
        ? t("knowledgeLoadError")
        : (resource.data?.title ??
          t(type === "context" ? "contextDetailTitle" : "documentReaderTitle"))
    }`;
    if (
      (resource.data || resource.error) &&
      document.activeElement === document.body
    )
      reader.current?.querySelector<HTMLHeadingElement>("h1")?.focus({
        preventScroll: true,
      });
  }, [resource.data, resource.error, type, t, isPanel]);
  const content = (
    <KnowledgeReaderView
      resource={resource}
      source={state}
      spaceId={spaceId}
      search={search}
      panel={isPanel}
      type={type}
      onDeleted={() => back(true)}
      onRetry={() => setAttempt((value) => value + 1)}
    />
  );
  return (
    <div
      ref={reader}
      onKeyDown={(event) => {
        if (
          event.key === "Escape" &&
          !event.defaultPrevented &&
          event.currentTarget.contains(event.target as Node)
        ) {
          event.preventDefault();
          const back = knowledgeReturnTarget(state, spaceId, search);
          navigate(back.to, {
            state:
              state && typeof state === "object" && "focusKnowledge" in state
                ? { focusKnowledge: state.focusKnowledge }
                : undefined,
          });
        }
      }}
    >
      {isPanel ? (
        <Sheet
          open={open}
          onOpenChange={(value) => {
            if (!value) closePanel();
          }}
        >
          <SheetContent
            className="knowledge-reader-sheet"
            showCloseButton={false}
            aria-describedby={undefined}
            onCloseAutoFocus={(event) => {
              event.preventDefault();
              if (closing.current) {
                closing.current = false;
                back();
              }
            }}
          >
            {content}
          </SheetContent>
        </Sheet>
      ) : (
        content
      )}
    </div>
  );
}
