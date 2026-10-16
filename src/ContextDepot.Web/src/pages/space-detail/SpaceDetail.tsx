import { useCallback, useEffect, useState } from "react";
import { Outlet, useLocation, useParams, useSearchParams } from "react-router";
import { useTranslation } from "react-i18next";
import { ChevronLeftIcon, SearchIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { RequestFeedback } from "@/components/feedback/RequestFeedback";
import { useRequestResource } from "@/hooks/use-request-resource";
import { getSpace } from "@/features/spaces/spaces-api";
import {
  PageHeader,
  PageHeaderSkeleton,
} from "@/components/content/PageHeader";
import { SpaceContents } from "@/features/spaces/SpaceContents";
import { SpaceKnowledgeSkeleton } from "@/features/spaces/SpaceKnowledgeList";
import { Link } from "react-router";
import { SpaceSwitcher } from "@/features/spaces/SpaceBrowser";
import { useSpaceDirectory } from "@/features/spaces/use-space-directory";
import "@/styles/spaces.css";
import { workspaceDirectoryParams } from "@/features/spaces/directory-query";
import { DirectoryParameter } from "@/features/spaces/directory-enums";

export function SpaceDetail() {
  const { t } = useTranslation();
  const { spaceId = "" } = useParams();
  const location = useLocation();
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
  const roots = useSpaceDirectory(undefined, DirectoryParameter.ReturnPage);
  const directory = workspaceDirectoryParams(params, true);
  const rootQuery = directory.size ? `?${directory}` : "";
  const data =
    detail.error && !detail.error.retryable ? undefined : detail.data;
  const space = data?.workspace;
  const actions = space && (
    <>
      <SpaceSwitcher roots={roots} selectedSpaceId={space.id} />
      <Button asChild variant="outline" size="sm">
        <Link
          to={`/search?${new URLSearchParams({ workspace: space.path })}`}
          state={{ focusSearch: true }}
          aria-label={t("spacesSearchHere")}
          title={t("spacesSearchHere")}
        >
          <SearchIcon data-icon="inline-start" aria-hidden="true" />
          {t("spacesSearchAction")}
        </Link>
      </Button>
    </>
  );
  return (
    <div className="spaces-workbench">
      <div>
        <div className="space-browser">
          <div className="space-browser-main">
            <nav
              className="space-detail-navigation"
              aria-label={t("spacesReturnDirectory")}
            >
              <Button
                asChild
                variant="ghost"
                size="sm"
                className="space-directory-back"
              >
                <Link
                  to={`/spaces${rootQuery}`}
                  aria-label={t("spacesReturnDirectory")}
                >
                  <ChevronLeftIcon
                    data-icon="inline-start"
                    aria-hidden="true"
                  />
                  {t("spacesDirectoryLink")}
                </Link>
              </Button>
            </nav>
            {detail.error && (
              <RequestFeedback
                title={t("spacesDetailError")}
                failure={detail.error}
                onRetry={() => setAttempt((value) => value + 1)}
                pending={detail.pending}
                stale={!!space}
              />
            )}
            {!space && detail.pending && !detail.error && (
              <div className="space-detail-skeleton" aria-busy="true">
                <span className="sr-only" role="status">
                  {t("loading")}
                </span>
                <div className="space-detail-heading">
                  <PageHeaderSkeleton eyebrow={false} />
                </div>
                <div className="space-content-toolbar" aria-hidden="true">
                  <Skeleton className="h-12 w-44" />
                  <div className="space-content-actions">
                    <Skeleton className="space-layout-switch h-11 w-24" />
                    <div className="space-toolbar-commands">
                      <Skeleton className="h-11 w-28" />
                      <Skeleton className="h-11 w-24" />
                    </div>
                  </div>
                </div>
                <SpaceKnowledgeSkeleton />
              </div>
            )}
            {space && (
              <>
                <div
                  className="space-detail-heading"
                  key={`heading:${space.id}`}
                >
                  <PageHeader
                    title={space.name}
                    description={space.description ?? ""}
                  />
                </div>
                <SpaceContents
                  key={`${space.id}:${location.state?.refreshKnowledge ?? ""}`}
                  spaceId={space.id}
                  actions={actions}
                />
              </>
            )}
          </div>
        </div>
      </div>
      <Outlet />
    </div>
  );
}
