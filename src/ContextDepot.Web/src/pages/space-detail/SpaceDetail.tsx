import { useKnowledgeReading } from "@/features/knowledge/knowledge-reading-context";
import { useEffect, useRef } from "react";
import {
  Link,
  Outlet,
  useLocation,
  useLoaderData,
  useNavigate,
  useRevalidator,
  useSearchParams,
} from "react-router";
import { useTranslation } from "react-i18next";
import { SearchIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { RequestFeedback } from "@/components/feedback/RequestFeedback";
import { SpaceDetailHeading } from "@/features/spaces/SpaceDetailHeading";
import { SpaceContents } from "@/features/spaces/SpaceContents";
import { SpaceSwitcher } from "@/features/spaces/SpaceBrowser";
import "@/styles/spaces.css";
import { workspaceDirectoryParams } from "@/features/spaces/directory-query";
import { SpaceBreadcrumbs } from "@/features/spaces/SpaceBreadcrumbs";
import {
  readSpacePanelPage,
  type SpaceRouteData,
} from "@/features/spaces/space-route-loader";

export function SpaceDetail() {
  const { t } = useTranslation();
  const workbench = useRef<HTMLDivElement>(null);
  const location = useLocation();
  const navigate = useNavigate();
  const revision = useKnowledgeReading()?.revision ?? 0;
  const previousRevision = useRef(revision);
  const { detail, panel } = useLoaderData<SpaceRouteData>();
  const { revalidate, state } = useRevalidator();
  const [params, setParams] = useSearchParams();
  useEffect(() => {
    if (!params.has("spaceView") && !params.has("page")) return;
    const next = new URLSearchParams(params);
    next.delete("spaceView");
    next.delete("page");
    setParams(next, { replace: true, preventScrollReset: true });
  }, [params, setParams]);
  useEffect(() => {
    if (previousRevision.current === revision) return;
    previousRevision.current = revision;
    void revalidate();
  }, [revision, revalidate]);
  const directory = workspaceDirectoryParams(params, true);
  const data = detail.data;
  const space = data?.workspace;
  function childCreated() {
    if (readSpacePanelPage(params, "childrenPage") > 1) {
      const next = new URLSearchParams(params);
      next.delete("childrenPage");
      setParams(next, { replace: true, preventScrollReset: true });
    } else void revalidate();
  }
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
          className="space-search-action"
          state={{ focusSearch: true }}
          aria-label={t("spacesSearchHere")}
          title={t("spacesSearchHere")}
        >
          <SearchIcon data-icon="inline-start" aria-hidden="true" />
          <span>{t("spacesSearchAction")}</span>
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
              <SpaceBreadcrumbs detail={data} directory={directory} />
              {space && <SpaceDetailHeading space={space} />}
            </div>
            {detail.error && (
              <RequestFeedback
                title={t("spacesDetailError")}
                failure={detail.error}
                onRetry={revalidate}
                pending={state === "loading"}
                stale={!!space}
                recoveryAction={
                  !detail.error.retryable
                    ? {
                        label: t("spacesReturnDirectory"),
                        onClick: () =>
                          void navigate(
                            `/spaces${directory.size ? `?${directory}` : ""}`,
                          ),
                      }
                    : undefined
                }
              />
            )}
            {space && (
              <SpaceContents
                key={`${space.id}:${location.state?.refreshKnowledge ?? revision}`}
                space={space}
                panel={panel}
                onChildCreated={childCreated}
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
