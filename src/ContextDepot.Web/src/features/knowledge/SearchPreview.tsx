import { KnowledgeCopyButton } from "./KnowledgeCopyButton";
import type { RefObject } from "react";
import { Link, useLocation } from "react-router";
import { LayersIcon } from "lucide-react";
import { knowledgePath } from "./knowledge-routes";
import { useTranslation } from "react-i18next";
import { ArrowLeftIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { RequestFeedback } from "@/components/feedback/RequestFeedback";
import type { Resource } from "@/hooks/use-request-resource";
import type { SearchHit, SearchWorkspace } from "./search-api";
import { type KnowledgePreview } from "@/features/knowledge/knowledge-models";
import { SearchEmpty, SearchPreviewContent } from "./SearchContent";
import { SearchPreviewSkeleton } from "./SearchSkeleton";

export function SearchPreview({
  item,
  resource,
  workspace,
  searchPending = false,
  onBack,
  onRetry,
  paneRef,
}: {
  item?: SearchHit;
  resource: Resource<KnowledgePreview>;
  workspace?: SearchWorkspace;
  searchPending?: boolean;
  onBack: () => void;
  onRetry: () => void;
  paneRef: RefObject<HTMLElement | null>;
}) {
  const { t } = useTranslation();
  const location = useLocation();
  return (
    <section
      className="search-preview-pane"
      aria-labelledby="search-preview-title"
      aria-busy={resource.pending || searchPending}
      tabIndex={-1}
      ref={paneRef}
    >
      <div className="search-pane-toolbar">
        <div className="search-reader-navigation">
          <Button
            className="search-return"
            variant="ghost"
            size="sm"
            aria-label={t("searchBackToList")}
            onClick={onBack}
          >
            <ArrowLeftIcon data-icon="inline-start" aria-hidden="true" />
            <span className="search-return-label">{t("searchBackToList")}</span>
          </Button>
          <h2 id="search-preview-title">{t("searchReaderTitle")}</h2>
        </div>
        <div className="search-preview-actions">
          <KnowledgeCopyButton
            detail={resource.error ? undefined : resource.data}
            pending={resource.pending || searchPending}
          />
          {workspace &&
            resource.data &&
            !resource.error &&
            !resource.pending &&
            !searchPending &&
            workspace.path === resource.data.workspace && (
              <Button asChild variant="outline" size="sm">
                <Link
                  to={knowledgePath(
                    resource.data.type,
                    resource.data.id,
                    workspace.id,
                  )}
                  state={{
                    from: location.pathname + location.search,
                    navigation: "spaces",
                  }}
                >
                  <LayersIcon data-icon="inline-start" aria-hidden="true" />
                  {t("searchViewInSpace")}
                </Link>
              </Button>
            )}
        </div>
      </div>
      <div
        className="search-preview-scroll"
        key={item ? `${item.type}:${item.id}` : "empty"}
      >
        {!item && searchPending ? (
          <SearchPreviewSkeleton />
        ) : !item ? (
          <SearchEmpty
            title={t("dialogSelect")}
            description={t("dialogSelectWhy")}
          />
        ) : resource.error ? (
          <RequestFeedback
            title={t("dialogPreviewError")}
            description={t("dialogPreviewErrorWhy")}
            failure={resource.error}
            pending={resource.pending}
            onRetry={onRetry}
            recoveryAction={{ label: t("searchBackToList"), onClick: onBack }}
          />
        ) : !resource.data ? (
          <SearchPreviewSkeleton />
        ) : (
          <article
            key={`${resource.data.type}:${resource.data.id}`}
            className="search-preview-document"
          >
            <SearchPreviewContent
              detail={resource.data}
              workspaceName={
                workspace?.name ??
                resource.data.workspaceName ??
                t("searchUnknownSpace")
              }
            />
          </article>
        )}
      </div>
    </section>
  );
}
