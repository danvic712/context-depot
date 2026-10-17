import { KnowledgeOpenLink } from "./KnowledgeOpenLink";
import { KnowledgeCopyButton } from "./KnowledgeCopyButton";
import type { RefObject } from "react";
import { useLocation } from "react-router";
import { ArrowUpRightIcon } from "lucide-react";
import { knowledgePath } from "./knowledge-routes";
import { useTranslation } from "react-i18next";
import { ArrowLeftIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { RequestFeedback } from "@/components/feedback/RequestFeedback";
import type { Resource } from "@/hooks/use-request-resource";
import type { SearchHit } from "./search-api";
import { type KnowledgePreview } from "@/features/knowledge/knowledge-models";
import { SearchEmpty, SearchPreviewContent } from "./SearchContent";
import { SearchPreviewSkeleton } from "./SearchSkeleton";

export function SearchPreview({
  item,
  resource,
  searchPending = false,
  onBack,
  onRetry,
  paneRef,
}: {
  item?: SearchHit;
  resource: Resource<KnowledgePreview>;
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
        <h2 id="search-preview-title">{t("searchContentPreview")}</h2>
        <div className="search-preview-actions">
          {item && (
            <Button asChild variant="ghost" size="icon">
              <KnowledgeOpenLink
                item={{ type: item.type, id: item.id, title: item.title }}
                to={knowledgePath(item.type, item.id)}
                state={{
                  from: location.pathname + location.search,
                  navigation: "search",
                }}
                aria-label={t("spacesOpenFullPage")}
                title={t("spacesOpenFullPage")}
              >
                <ArrowUpRightIcon aria-hidden="true" />
              </KnowledgeOpenLink>
            </Button>
          )}
          <Button
            className="search-return"
            variant="outline"
            size="sm"
            onClick={onBack}
          >
            <ArrowLeftIcon data-icon="inline-start" />
            {t("dialogBack")}
          </Button>
          <KnowledgeCopyButton
            detail={resource.error ? undefined : resource.data}
            pending={resource.pending || searchPending}
          />
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
            recoveryAction={{ label: t("dialogBack"), onClick: onBack }}
          />
        ) : !resource.data ? (
          <SearchPreviewSkeleton />
        ) : (
          <SearchPreviewContent detail={resource.data} />
        )}
      </div>
    </section>
  );
}
