import { useLayoutEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { RequestFeedback } from "@/components/feedback/RequestFeedback";
import type { Resource } from "@/hooks/use-request-resource";
import { knowledgeKey, type KnowledgePreview } from "./knowledge-models";
import type { SearchHit } from "./search-api";
import { LoaderCircleIcon } from "lucide-react";
import { SearchEmpty, SearchPreviewContent } from "./SearchContent";
import { createPreviewSwitchMotion } from "./preview-switch-motion";

export function SearchDialogPreview({
  item,
  detail,
  searchPending = false,
  workspaceNames,
  onRetry,
  onBack,
}: {
  item: SearchHit | undefined;
  detail: Resource<KnowledgePreview>;
  searchPending?: boolean;
  workspaceNames: Map<string, string>;
  onRetry: () => void;
  onBack: () => void;
}) {
  const { t } = useTranslation();
  const [previous, setPrevious] = useState(detail.data);
  if (detail.data && detail.data !== previous) setPrevious(detail.data);
  const outgoing =
    !!item && detail.pending && !detail.error && !detail.data && !!previous;
  const shown = detail.data ?? (outgoing ? previous : undefined);
  const loading =
    (!!item && detail.pending && !detail.error) || (!item && searchPending);
  const ref = useRef<HTMLDivElement>(null);
  const statusRef = useRef<HTMLDivElement>(null);
  const motion = useRef<ReturnType<typeof createPreviewSwitchMotion> | null>(
    null,
  );
  useLayoutEffect(() => {
    if (!ref.current || !statusRef.current) return;
    motion.current = createPreviewSwitchMotion(ref.current, statusRef.current);
    return () => {
      motion.current?.dispose();
      motion.current = null;
    };
  }, []);
  const identity = detail.data ? knowledgeKey(detail.data) : undefined;
  useLayoutEffect(() => {
    motion.current?.update(outgoing, identity, loading);
  }, [outgoing, identity, loading]);
  return (
    <div className="search-preview-region">
      <div className="search-preview-scroll">
        <div ref={ref} className="search-preview-content">
          {!item && searchPending ? null : !item ? (
            <SearchEmpty
              title={t("dialogSelect")}
              description={t("dialogSelectWhy")}
            />
          ) : detail.error ? (
            <RequestFeedback
              title={t("dialogPreviewError")}
              failure={detail.error}
              pending={detail.pending}
              onRetry={onRetry}
              recoveryAction={{ label: t("dialogBack"), onClick: onBack }}
            />
          ) : shown ? (
            <div
              aria-hidden={outgoing || undefined}
              inert={outgoing || undefined}
            >
              <SearchPreviewContent
                detail={shown}
                workspaceName={
                  workspaceNames.get(shown.workspace) ??
                  shown.workspaceName ??
                  t("searchUnknownSpace")
                }
              />
            </div>
          ) : null}
        </div>
      </div>
      <div
        ref={statusRef}
        className="search-preview-loading"
        aria-hidden={!loading}
      >
        <div
          className="search-preview-status"
          role={loading ? "status" : undefined}
          aria-live="polite"
          aria-atomic="true"
        >
          <LoaderCircleIcon
            className="motion-safe:animate-spin"
            aria-hidden="true"
          />
          <span>{t(item ? "dialogOpeningPreview" : "dialogSearching")}</span>
          {item && <strong>{item.title}</strong>}
        </div>
      </div>
    </div>
  );
}
