import { ChevronRightIcon, FolderIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { Link, useLocation } from "react-router";
import { Skeleton } from "@/components/ui/skeleton";
import type { Space } from "./spaces-api";
import {
  DirectoryParameter,
  directoryScrollStorageKey,
} from "./directory-enums";
import {
  workspaceDetailTarget,
  workspaceDirectoryParams,
} from "./directory-query";

export function WorkspaceRow({
  space,
  directoryPage,
}: {
  space: Space;
  directoryPage?: number;
}) {
  const { t, i18n } = useTranslation();
  const location = useLocation();
  const params = new URLSearchParams(location.search);
  const directory = workspaceDirectoryParams(
    params,
    location.pathname !== "/spaces",
  );
  if (directoryPage && directoryPage > 1)
    directory.set(DirectoryParameter.Page, String(directoryPage));
  const locale = i18n.resolvedLanguage;
  const contentCount = space.contextCount + space.documentCount;
  const number = new Intl.NumberFormat(locale);
  return (
    <Link
      className="workspace-row"
      to={workspaceDetailTarget(space.id, directory)}
      onClick={(event) => {
        if (location.pathname !== "/spaces") return;
        try {
          sessionStorage.setItem(
            directoryScrollStorageKey,
            JSON.stringify({
              url: `/spaces${directory.size ? `?${directory}` : ""}`,
              top: window.scrollY,
              listTop:
                event.currentTarget.closest(".workspace-list-panel")
                  ?.scrollTop ?? 0,
            }),
          );
        } catch {
          /* Browsing remains available when storage is blocked. */
        }
      }}
      state={{
        from: location.pathname + location.search,
        navigation: "spaces",
      }}
    >
      <span className="workspace-row-icon" aria-hidden="true">
        <FolderIcon />
      </span>
      <div className="workspace-row-identity">
        <div className="workspace-row-title">
          <h3 title={space.name}>{space.name}</h3>
        </div>
        {space.description && (
          <p title={space.description}>{space.description}</p>
        )}
      </div>
      <span
        className="workspace-row-summary"
        data-empty={contentCount === 0 || undefined}
      >
        {contentCount > 0
          ? t("spacesKnowledgeCount", {
              count: contentCount,
              value: number.format(contentCount),
            })
          : t("spacesFilterEmpty")}
      </span>
      <ChevronRightIcon className="workspace-row-arrow" aria-hidden="true" />
    </Link>
  );
}

export function WorkspaceRowSkeleton() {
  return (
    <div className="workspace-row workspace-row-skeleton" aria-hidden="true">
      <Skeleton className="workspace-row-icon" />
      <div className="workspace-row-identity">
        <div className="workspace-row-title">
          <Skeleton className="workspace-title-skeleton" />
        </div>
        <Skeleton className="workspace-description-skeleton" />
      </div>
      <Skeleton className="workspace-row-summary workspace-summary-skeleton" />
      <Skeleton className="workspace-row-arrow" />
    </div>
  );
}
