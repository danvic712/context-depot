import { useCallback, useEffect, useState } from "react";
import { Link, useNavigate, useParams, useSearchParams } from "react-router";
import { useTranslation } from "react-i18next";
import { ChevronRightIcon, SearchIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { RequestFeedback } from "@/components/feedback/RequestFeedback";
import { useRequestResource } from "@/hooks/use-request-resource";
import { getSpace } from "@/features/spaces/spaces-api";
import { WorkspaceCounts } from "@/features/spaces/WorkspaceCard";
import {
  PageHeader,
  PageHeaderSkeleton,
} from "@/components/content/PageHeader";
import { SpaceKnowledge } from "@/features/spaces/SpaceKnowledge";
import { SpaceKnowledgeSkeleton } from "@/features/spaces/SpaceKnowledgeList";
import { SpaceBrowser } from "@/features/spaces/SpaceBrowser";
import { useSpaceDirectory } from "@/features/spaces/use-space-directory";
import "@/styles/spaces.css";

export function SpaceDetail() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { spaceId = "" } = useParams();
  const [params, setParams] = useSearchParams();
  useEffect(() => {
    if (!params.has("spaceView") && !params.has("page")) return;
    const next = new URLSearchParams(params);
    next.delete("spaceView");
    next.delete("page");
    setParams(next, { replace: true, preventScrollReset: true });
  }, [params, setParams]);
  const [attempt, setAttempt] = useState(0);
  const load = useCallback(
    (signal: AbortSignal) => getSpace(spaceId, signal),
    [spaceId],
  );
  const detail = useRequestResource(spaceId, attempt, load);
  const roots = useSpaceDirectory(undefined, "directoryPage");
  const rootQuery = roots.page > 1 ? `?page=${roots.page}` : "";
  const data =
    detail.error && !detail.error.retryable ? undefined : detail.data;
  const space = data?.workspace;
  const search = space
    ? `/search?${new URLSearchParams({ workspace: space.path })}`
    : "/search";
  return (
    <div className="spaces-workbench">
      <SpaceBrowser roots={roots} selectedSpaceId={space?.id}>
        <nav className="space-breadcrumbs" aria-label={t("spacesBreadcrumb")}>
          <Link to={`/spaces${rootQuery}`}>{t("spaces")}</Link>
          {space && (
            <span>
              <ChevronRightIcon aria-hidden="true" />
              <span aria-current="page">{space.name}</span>
            </span>
          )}
        </nav>
        {detail.error && (
          <RequestFeedback
            title={t("spacesDetailError")}
            failure={detail.error}
            onRetry={() => setAttempt((value) => value + 1)}
            pending={detail.pending}
            stale={!!space}
            recoveryAction={{
              label: t("spacesReturnDirectory"),
              onClick: () => void navigate(`/spaces${rootQuery}`),
            }}
          />
        )}
        {!space && detail.pending && !detail.error && (
          <div className="space-detail-skeleton" aria-busy="true">
            <span className="sr-only" role="status">
              {t("loading")}
            </span>
            <PageHeaderSkeleton />
            <SpaceKnowledgeSkeleton />
          </div>
        )}
        {space && (
          <>
            <PageHeader
              title={space.name}
              description={space.description ?? ""}
              actions={
                <Button asChild variant="outline">
                  <Link to={search} state={{ focusSearch: true }}>
                    <SearchIcon aria-hidden="true" />
                    {t("spacesSearchHere")}
                  </Link>
                </Button>
              }
            >
              <div className="space-detail-meta">
                <code className="space-detail-path" title={space.path}>
                  {space.path}
                </code>
                <WorkspaceCounts space={space} />
              </div>
            </PageHeader>
            <SpaceKnowledge key={space.id} spaceId={space.id} />
          </>
        )}
      </SpaceBrowser>
    </div>
  );
}
