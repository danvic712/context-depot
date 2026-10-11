import { useCallback, useEffect, useRef, useState } from "react";
import { Link, useLocation } from "react-router";
import { useTranslation } from "react-i18next";
import { ExpandIcon, XIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { RequestFeedback } from "@/components/feedback/RequestFeedback";
import { useRequestResource } from "@/hooks/use-request-resource";
import type { KnowledgeSummary } from "@/features/home/home-api";
import { getKnowledgePreview, hitKey } from "@/features/knowledge/search-api";
import { KnowledgeContent } from "@/features/knowledge/KnowledgeContent";
import { KnowledgeContentSkeleton } from "@/features/knowledge/KnowledgeReaderSkeleton";
import { SearchCopyButton } from "@/features/knowledge/SearchContent";

export function SpaceReader({
  item,
  onClose,
}: {
  item: KnowledgeSummary;
  onClose: () => void;
}) {
  const { t } = useTranslation();
  const location = useLocation();
  const panel = useRef<HTMLElement>(null);
  const [attempt, setAttempt] = useState(0);
  const load = useCallback(
    async (signal: AbortSignal) => {
      const detail = await getKnowledgePreview(item, signal);
      if (detail.workspace !== item.workspace.path)
        throw new Error("Invalid workspace knowledge");
      return detail;
    },
    [item],
  );
  const resource = useRequestResource(hitKey(item), attempt, load);
  const detail = resource.error ? undefined : resource.data;
  useEffect(() => {
    panel.current?.focus({ preventScroll: true });
    if (window.matchMedia("(max-width: 1279px)").matches)
      panel.current?.scrollIntoView({ block: "start" });
  }, []);
  return (
    <section
      ref={panel}
      className="space-reader"
      tabIndex={-1}
      aria-label={t("spacesReader")}
    >
      <div className="space-reader-toolbar">
        <span>{t("spacesReader")}</span>
        <div>
          <SearchCopyButton detail={detail} pending={resource.pending} />
          <Button asChild variant="outline" size="icon">
            <Link
              to={`/${item.type === "context" ? "contexts" : "documents"}/${encodeURIComponent(item.id)}`}
              state={{
                from: location.pathname + location.search,
                navigation: "spaces",
              }}
              aria-label={t("spacesOpenFullPage")}
              title={t("spacesOpenFullPage")}
            >
              <ExpandIcon aria-hidden="true" />
            </Link>
          </Button>
          <Button
            variant="outline"
            size="icon"
            onClick={onClose}
            aria-label={t("spacesCloseReader")}
            title={t("spacesCloseReader")}
          >
            <XIcon aria-hidden="true" />
          </Button>
        </div>
      </div>
      <div className="space-reader-body" aria-busy={resource.pending}>
        {resource.error && (
          <RequestFeedback
            title={t("spacesReaderError")}
            failure={resource.error}
            onRetry={() => setAttempt((value) => value + 1)}
            pending={resource.pending}
            recoveryAction={{ label: t("spacesReturnList"), onClick: onClose }}
          />
        )}
        {!detail && resource.pending && !resource.error && (
          <>
            <span className="sr-only" role="status">
              {t("loading")}
            </span>
            <KnowledgeContentSkeleton />
          </>
        )}
        {detail && <KnowledgeContent detail={detail} />}
      </div>
    </section>
  );
}
