import { useEffect } from "react";
import { useSearchParams } from "react-router";
import { FolderIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { RequestFeedback } from "@/components/feedback/RequestFeedback";
import {
  Empty,
  EmptyHeader,
  EmptyMedia,
  EmptyTitle,
  EmptyDescription,
} from "@/components/ui/empty";
import { WorkspaceRow } from "./WorkspaceRow";
import { SpaceListSkeleton } from "./SpaceDirectory";
import {
  SpaceDirectoryFooter,
  WorkspaceListPanel,
} from "./SpaceDirectoryLayout";
import { useWorkspaceChildren } from "./use-space-directory";
import { readSpacePanelPage, type SpacePageSeed } from "./space-route-loader";
import type { SpaceDirectory } from "./spaces-api";

export function SpaceChildren({
  spaceId,
  seed,
}: {
  spaceId: string;
  seed?: SpacePageSeed<SpaceDirectory>;
}) {
  const { t } = useTranslation();
  const [params, setParams] = useSearchParams();
  const page = readSpacePanelPage(params, "childrenPage");
  const { data, pending, error, retry } = useWorkspaceChildren(
    spaceId,
    page,
    undefined,
    seed,
  );
  const lastPage = data
    ? Math.max(1, Math.ceil(data.totalCount / data.pageSize))
    : page;
  useEffect(() => {
    if (!data || pending || error || page <= lastPage) return;
    const next = new URLSearchParams(params);
    if (lastPage === 1) next.delete("childrenPage");
    else next.set("childrenPage", String(lastPage));
    setParams(next, { replace: true, preventScrollReset: true });
  }, [data, pending, error, page, lastPage, params, setParams]);

  return (
    <section aria-label={t("spacesChildrenTab")} aria-busy={pending}>
      {error && (
        <RequestFeedback
          title={t("spacesChildrenError")}
          failure={error}
          pending={pending}
          onRetry={retry}
          stale={!!data}
        />
      )}
      {!data && pending && (
        <span className="sr-only" role="status">
          {t("loading")}
        </span>
      )}
      {data?.items.length || (!data && pending) ? (
        <WorkspaceListPanel>
          <ul className="workspace-list">
            {!data && <SpaceListSkeleton count={3} />}
            {data?.items.map((space) => (
              <li key={space.id}>
                <WorkspaceRow space={space} />
              </li>
            ))}
          </ul>
        </WorkspaceListPanel>
      ) : null}
      {data?.totalCount === 0 && !pending && !error && (
        <Empty className="spaces-empty space-children-empty">
          <EmptyHeader>
            <EmptyMedia className="spaces-empty-art">
              <FolderIcon aria-hidden="true" />
            </EmptyMedia>
            <EmptyTitle>{t("spacesNoChildren")}</EmptyTitle>
            <EmptyDescription>
              {t("spacesNoChildrenDescription")}
            </EmptyDescription>
          </EmptyHeader>
        </Empty>
      )}
      {data && (
        <SpaceDirectoryFooter
          totalCount={data.totalCount}
          page={data.page}
          pageSize={data.pageSize}
          itemCount={data.items.length}
          onPageChange={(value) => {
            const next = new URLSearchParams(params);
            if (value === 1) next.delete("childrenPage");
            else next.set("childrenPage", String(value));
            setParams(next, { preventScrollReset: true });
          }}
        />
      )}
    </section>
  );
}
