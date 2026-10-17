import { useKnowledgeReading } from "@/features/knowledge/knowledge-reading-context";
import { useCallback, useEffect, useRef, useState } from "react";
import { Outlet, useLocation, useParams, useSearchParams } from "react-router";
import { useTranslation } from "react-i18next";
import { ChevronLeftIcon, SearchIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { RequestFeedback } from "@/components/feedback/RequestFeedback";
import { useRequestResource } from "@/hooks/use-request-resource";
import { getSpace } from "@/features/spaces/spaces-api";
import {
  SpaceDetailHeading,
  SpaceDetailHeadingSkeleton,
} from "@/features/spaces/SpaceDetailHeading";
import { SpaceContents } from "@/features/spaces/SpaceContents";
import { SpaceKnowledgeSkeleton } from "@/features/spaces/SpaceKnowledgeList";
import { Link } from "react-router";
import { SpaceSwitcher } from "@/features/spaces/SpaceBrowser";
import "@/styles/spaces.css";
import { workspaceDirectoryParams } from "@/features/spaces/directory-query";

export function SpaceDetail() {
  const { t } = useTranslation();
  const workbench = useRef<HTMLDivElement>(null);
  const { spaceId = "" } = useParams();
  const location = useLocation();
  const revision = useKnowledgeReading()?.revision ?? 0;
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
  const detail = useRequestResource(spaceId, attempt + revision, load);
  const directory = workspaceDirectoryParams(params, true);
  const rootQuery = directory.size ? `?${directory}` : "";
  const data =
    detail.error && !detail.error.retryable ? undefined : detail.data;
  const space = data?.workspace;
  useEffect(() => {
    if (
      !space ||
      !location.state?.focusSpace ||
      document.activeElement !== document.body
    )
      return;
    const heading = workbench.current?.querySelector<HTMLHeadingElement>(
      ".space-detail-heading h1",
    );
    if (heading) {
      heading.tabIndex = -1;
      heading.focus({ preventScroll: true });
    }
  }, [space, location.state?.focusSpace]);
  const actions = space && (
    <>
      <SpaceSwitcher selectedSpaceId={space.id} />
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
    <div className="spaces-workbench" ref={workbench}>
      <div>
        <div className="space-browser">
          <div className="space-browser-main">
            <div className="space-detail-header">
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
                    title={t("spacesReturnDirectory")}
                  >
                    <ChevronLeftIcon
                      data-icon="inline-start"
                      aria-hidden="true"
                    />
                    {t("spacesDirectoryLink")}
                  </Link>
                </Button>
              </nav>
              {space ? (
                <SpaceDetailHeading key={space.id} space={space} />
              ) : detail.pending && !detail.error ? (
                <SpaceDetailHeadingSkeleton />
              ) : null}
            </div>
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
              <SpaceContents
                key={`${space.id}:${location.state?.refreshKnowledge ?? revision}`}
                spaceId={space.id}
                contentCount={space.contextCount + space.documentCount}
                actions={actions}
              />
            )}
          </div>
        </div>
      </div>
      <Outlet />
    </div>
  );
}
