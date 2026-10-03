import { useCallback, useEffect, useRef, useState } from "react";
import { Link, useLocation, useParams } from "react-router";
import { useTranslation } from "react-i18next";
import { ArrowLeftIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { RequestFeedback } from "@/components/feedback/RequestFeedback";
import {
  useRequestResource,
  type Resource,
} from "@/hooks/use-request-resource";
import { KnowledgeContent } from "./KnowledgeContent";
import { KnowledgeContentSkeleton } from "./KnowledgeReaderSkeleton";
import { SearchCopyButton } from "./SearchContent";
import { knowledgeReturnTarget } from "./knowledge-navigation";
import {
  getKnowledgePreview,
  type KnowledgePreview,
  type KnowledgeType,
} from "./search-api";
import "@/styles/knowledge-reader.css";

export function KnowledgeReaderView({
  resource,
  source,
  onRetry,
}: {
  resource: Resource<KnowledgePreview>;
  source?: unknown;
  onRetry: () => void;
}) {
  const { t } = useTranslation();
  const back = knowledgeReturnTarget(source);
  const errorTitle =
    resource.error?.kind === "notFound"
      ? "knowledgeNotFound"
      : resource.error?.kind === "forbidden"
        ? "knowledgeForbidden"
        : "knowledgeLoadError";
  return (
    <section className="knowledge-reader" aria-busy={resource.pending}>
      <div className="knowledge-reader-toolbar">
        <Button asChild variant="outline">
          <Link to={back.to}>
            <ArrowLeftIcon aria-hidden="true" />
            {t(back.label)}
          </Link>
        </Button>
        <SearchCopyButton
          detail={resource.error ? undefined : resource.data}
          pending={resource.pending}
        />
      </div>
      <article className="knowledge-reader-document">
        {resource.error ? (
          <div className="knowledge-reader-error">
            <h1 tabIndex={-1}>{t(errorTitle)}</h1>
            <RequestFeedback
              title={t("knowledgeLoadError")}
              failure={resource.error}
              pending={resource.pending}
              onRetry={onRetry}
            />
          </div>
        ) : resource.data ? (
          <KnowledgeContent detail={resource.data} headingLevel={1} />
        ) : (
          <div role="status" aria-label={t("loading")}>
            <KnowledgeContentSkeleton />
          </div>
        )}
      </article>
    </section>
  );
}

export function KnowledgeReader({ type }: { type: KnowledgeType }) {
  const { knowledgeId = "" } = useParams();
  const { state } = useLocation();
  const { t } = useTranslation();
  const [attempt, setAttempt] = useState(0);
  const reader = useRef<HTMLDivElement>(null);
  const load = useCallback(
    (signal: AbortSignal) =>
      getKnowledgePreview({ type, id: knowledgeId }, signal),
    [type, knowledgeId],
  );
  const resource = useRequestResource(`${type}:${knowledgeId}`, attempt, load);
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
  }, [resource.data, resource.error, type, t]);
  return (
    <div ref={reader}>
      <KnowledgeReaderView
        resource={resource}
        source={state}
        onRetry={() => setAttempt((value) => value + 1)}
      />
    </div>
  );
}
