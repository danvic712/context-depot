import { useCallback, useState } from "react";
import { Link, useParams } from "react-router";
import { useTranslation } from "react-i18next";
import { ChevronRightIcon, FolderOpenIcon, SearchIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { RequestFeedback } from "@/components/feedback/RequestFeedback";
import { useRequestResource } from "@/hooks/use-request-resource";
import { getSpace } from "@/features/spaces/spaces-api";
import { SpaceDirectory } from "@/features/spaces/SpaceDirectory";
import { WorkspaceCounts } from "@/features/spaces/WorkspaceCard";
import { PageHeader } from "@/components/content/PageHeader";
import { useSpaceDirectory } from "@/features/spaces/use-space-directory";
import "@/styles/spaces.css";

export function SpaceDetail() {
  const { t } = useTranslation();
  const { spaceId = "" } = useParams();
  const [attempt, setAttempt] = useState(0);
  const load = useCallback(
    (signal: AbortSignal) => getSpace(spaceId, signal),
    [spaceId],
  );
  const detail = useRequestResource(spaceId, attempt, load);
  const children = useSpaceDirectory(spaceId);
  const space = detail.data?.workspace;
  const search = space
    ? `/search?${new URLSearchParams({ workspace: space.path })}`
    : "/search";
  return (
    <div className="spaces-workbench">
      <nav className="space-breadcrumbs" aria-label={t("spacesBreadcrumb")}>
        <Link to="/spaces">{t("spaces")}</Link>
        {detail.data?.ancestors.map((ancestor) => (
          <span key={ancestor.id}>
            <ChevronRightIcon aria-hidden="true" />
            <Link to={`/spaces/${ancestor.id}`}>{ancestor.name}</Link>
          </span>
        ))}
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
        />
      )}
      {!space && detail.pending && !detail.error && (
        <div className="space-detail-skeleton" aria-busy="true">
          <span className="sr-only" role="status">
            {t("loading")}
          </span>
          <Skeleton className="size-12 rounded-xl" />
          <Skeleton className="mt-6 h-10 w-2/3" />
          <Skeleton className="mt-4 h-4 w-3/4" />
          <Skeleton className="mt-4 h-4 w-1/3" />
        </div>
      )}
      {space && (
        <>
          <PageHeader
            eyebrow={
              <span className="space-detail-icon">
                <FolderOpenIcon aria-hidden="true" />
              </span>
            }
            title={space.name}
            description={space.description || t("spaceNoDescription")}
            actions={
              <Button asChild variant="outline">
                <Link to={search} state={{ focusSearch: true }}>
                  <SearchIcon aria-hidden="true" />
                  {t("spacesSearchHere")}
                </Link>
              </Button>
            }
          >
            <span className="space-detail-path">{space.path}</span>
          </PageHeader>
          <section
            className="space-knowledge"
            aria-labelledby="space-knowledge-title"
          >
            <div>
              <h2 id="space-knowledge-title">{t("spacesKnowledgeTitle")}</h2>
              <p>{t("spacesKnowledgeDescription")}</p>
            </div>
            <WorkspaceCounts space={space} />
          </section>
          <SpaceDirectory resource={children} />
        </>
      )}
    </div>
  );
}
