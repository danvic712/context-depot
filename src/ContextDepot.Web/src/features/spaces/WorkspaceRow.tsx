import { ChevronRightIcon, FolderIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { Link, useLocation } from "react-router";
import { Skeleton } from "@/components/ui/skeleton";
import { WorkspaceCounts } from "./WorkspaceCard";
import type { Space } from "./spaces-api";

export function WorkspaceRow({ space }: { space: Space }) {
  const { t, i18n } = useTranslation();
  const location = useLocation();
  const params = new URLSearchParams(location.search);
  const directoryPage = params.get(
    location.pathname === "/spaces" ? "page" : "directoryPage",
  );
  const query =
    directoryPage && Number(directoryPage) > 1
      ? `?directoryPage=${encodeURIComponent(directoryPage)}`
      : "";
  const locale = i18n.resolvedLanguage;
  const date = new Date(space.activityAt);
  return (
    <Link
      className="workspace-row"
      to={`/spaces/${space.id}${query}`}
      state={{
        from: location.pathname + location.search,
        navigation: "spaces",
      }}
    >
      <span className="workspace-row-icon" aria-hidden="true">
        <FolderIcon />
      </span>
      <div className="workspace-row-identity">
        <h3 title={space.name}>{space.name}</h3>
        {space.description && (
          <p title={space.description}>{space.description}</p>
        )}
        <code title={space.path}>{space.path}</code>
      </div>
      <div className="workspace-row-counts">
        <WorkspaceCounts space={space} />
      </div>
      <time
        className="workspace-row-activity"
        dateTime={space.activityAt}
        title={new Intl.DateTimeFormat(locale, {
          dateStyle: "full",
          timeStyle: "short",
        }).format(date)}
      >
        <span className="workspace-row-activity-label">
          {t("spacesActivity")}
        </span>
        {new Intl.DateTimeFormat(locale, {
          year: "numeric",
          month: "short",
          day: "numeric",
        }).format(date)}
      </time>
      <ChevronRightIcon className="workspace-row-arrow" aria-hidden="true" />
    </Link>
  );
}

export function WorkspaceRowSkeleton() {
  return (
    <div className="workspace-row workspace-row-skeleton" aria-hidden="true">
      <Skeleton className="workspace-row-icon" />
      <div className="workspace-row-identity">
        <Skeleton className="h-5 w-2/3" />
        <Skeleton className="mt-2 h-3 w-4/5" />
        <Skeleton className="mt-2 h-3 w-1/2" />
      </div>
      <div className="workspace-row-counts">
        <Skeleton className="h-3 w-28" />
      </div>
      <Skeleton className="workspace-row-activity h-3 w-24" />
    </div>
  );
}
