import { ChevronRightIcon, FolderIcon, FolderTreeIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { Link, useLocation } from "react-router";
import { Skeleton } from "@/components/ui/skeleton";
import type { WorkspaceSummary } from "@/features/home/home-api";
import "@/styles/workspace-card.css";

type Workspace = Pick<
  WorkspaceSummary,
  "id" | "name" | "description" | "path" | "contextCount" | "documentCount"
> & { subspaceCount?: number };

export function WorkspaceCounts({
  space,
}: {
  space: Pick<Workspace, "contextCount" | "documentCount">;
}) {
  const { t, i18n } = useTranslation();
  const number = new Intl.NumberFormat(i18n.resolvedLanguage);
  return (
    <div className="space-counts" title={t("spaceCountsHint")}>
      <span>
        {t("spacesContextCount", {
          count: space.contextCount,
          value: number.format(space.contextCount),
        })}
      </span>
      <span>
        {t("spacesDocumentCount", {
          count: space.documentCount,
          value: number.format(space.documentCount),
        })}
      </span>
    </div>
  );
}

export function WorkspaceCard({
  space,
  navigation,
}: {
  space: Workspace;
  navigation: "home" | "spaces";
}) {
  const { t, i18n } = useTranslation();
  const location = useLocation();
  return (
    <Link
      className="space-card"
      to={`/spaces/${space.id}`}
      state={{ from: location.pathname + location.search, navigation }}
    >
      <div className="space-card-top">
        <span className="space-card-icon">
          <FolderIcon aria-hidden="true" />
        </span>
        {!!space.subspaceCount && (
          <span className="space-child-count">
            <FolderTreeIcon aria-hidden="true" />
            {t("spacesSubspaceCount", {
              count: space.subspaceCount,
              value: new Intl.NumberFormat(i18n.resolvedLanguage).format(
                space.subspaceCount,
              ),
            })}
          </span>
        )}
      </div>
      <h3 title={space.name}>{space.name}</h3>
      <p
        className="space-card-description"
        title={space.description ?? undefined}
      >
        {space.description || t("spaceNoDescription")}
      </p>
      <div className="space-card-footer">
        <span className="space-card-path" title={space.path}>
          {space.path}
        </span>
        <WorkspaceCounts space={space} />
        <ChevronRightIcon className="space-card-arrow" aria-hidden="true" />
      </div>
    </Link>
  );
}

export function WorkspaceCardSkeleton() {
  return (
    <div className="space-card space-card-skeleton" aria-hidden="true">
      <Skeleton className="size-9 rounded-lg" />
      <Skeleton className="mt-3 h-5 w-2/3" />
      <Skeleton className="mt-2 h-4 w-full" />
      <div className="space-card-footer">
        <Skeleton className="h-3 w-1/2" />
        <Skeleton className="mt-2 h-3 w-3/4" />
      </div>
    </div>
  );
}
