import { useEffect, useRef, useState, type ReactNode } from "react";
import { Link, useSearchParams } from "react-router";
import { useTranslation } from "react-i18next";
import { FolderIcon, LayoutListIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { PaginationBar } from "@/components/content/PaginationBar";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { Skeleton } from "@/components/ui/skeleton";
import { RequestFeedback } from "@/components/feedback/RequestFeedback";
import type { useSpaceDirectory } from "./use-space-directory";
import {
  workspaceDirectoryParams,
  workspaceDetailTarget,
} from "./directory-query";
import { DirectoryParameter } from "./directory-enums";

type Directory = ReturnType<typeof useSpaceDirectory>;

function SpaceNavigation({
  roots,
  selectedSpaceId,
  onNavigate,
}: {
  roots: Directory;
  selectedSpaceId?: string;
  onNavigate?: () => void;
}) {
  const { t, i18n } = useTranslation();
  const [params] = useSearchParams();
  const data = roots.error && !roots.error.retryable ? undefined : roots.data;
  const pages = Math.max(
    1,
    Math.ceil((data?.totalCount ?? 0) / (data?.pageSize ?? 12)),
  );
  const home = useRef<HTMLAnchorElement>(null);
  const previousPage = useRef<number | undefined>(undefined);
  const loadedPage = data?.page;
  useEffect(() => {
    if (loadedPage === undefined) return;
    if (
      previousPage.current !== undefined &&
      previousPage.current !== loadedPage &&
      document.activeElement === document.body &&
      home.current?.getClientRects().length
    )
      home.current.focus({ preventScroll: true });
    previousPage.current = loadedPage;
  }, [loadedPage]);
  function target(id?: string) {
    const directory = workspaceDirectoryParams(params, true);
    if (roots.page > 1)
      directory.set(DirectoryParameter.Page, String(roots.page));
    else directory.delete(DirectoryParameter.Page);
    return id
      ? workspaceDetailTarget(id, directory)
      : `/spaces${directory.size ? `?${directory}` : ""}`;
  }
  return (
    <nav className="space-navigation" aria-label={t("spacesExplorer")}>
      <Link
        ref={home}
        className="space-navigation-home"
        to={target()}
        onClick={onNavigate}
      >
        <LayoutListIcon aria-hidden="true" />
        {t("spacesAllSpaces")}
        {data && (
          <span>
            {new Intl.NumberFormat(i18n.resolvedLanguage).format(
              data.totalCount,
            )}
          </span>
        )}
      </Link>
      <div className="space-navigation-group" aria-busy={roots.pending}>
        {roots.error && (
          <RequestFeedback
            title={t("spacesLoadError")}
            failure={roots.error}
            onRetry={roots.refresh}
            pending={roots.pending}
            stale={!!data}
            compact
          />
        )}
        {!data && roots.pending && !roots.error && (
          <>
            <span className="sr-only" role="status">
              {t("loading")}
            </span>
            {Array.from({ length: 4 }, (_, index) => (
              <Skeleton key={index} className="space-navigation-skeleton" />
            ))}
          </>
        )}
        {data && (
          <ul className="space-navigation-list">
            {data.items.map((space) => (
              <li key={space.id}>
                <Link
                  className="space-navigation-link"
                  to={target(space.id)}
                  onClick={onNavigate}
                  aria-current={
                    space.id === selectedSpaceId ? "page" : undefined
                  }
                >
                  <FolderIcon aria-hidden="true" />
                  <span>
                    <strong title={space.name}>{space.name}</strong>
                    <code title={space.path}>{space.path}</code>
                  </span>
                </Link>
              </li>
            ))}
          </ul>
        )}
        {data?.totalCount === 0 && (
          <p className="space-navigation-note">{t("spacesExplorerEmpty")}</p>
        )}
        {data && pages > 1 && (
          <PaginationBar
            compact
            page={roots.page}
            pageSize={data.pageSize}
            totalCount={data.totalCount}
            ariaLabel={t("spacesPagination")}
            onPageChange={roots.changePage}
            disabled={roots.pending}
          />
        )}
      </div>
    </nav>
  );
}

export function SpaceBrowser({
  roots,
  selectedSpaceId,
  children,
}: {
  roots: Directory;
  selectedSpaceId?: string;
  children: ReactNode;
}) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  return (
    <div className="space-browser">
      <aside className="space-browser-sidebar">
        <SpaceNavigation roots={roots} selectedSpaceId={selectedSpaceId} />
      </aside>
      <div className="space-browser-main">
        <Dialog open={open} onOpenChange={setOpen}>
          <DialogTrigger asChild>
            <Button className="space-browser-mobile-trigger" variant="outline">
              <LayoutListIcon aria-hidden="true" />
              {t("spacesExplorer")}
            </Button>
          </DialogTrigger>
          <DialogContent
            className="space-browser-dialog"
            aria-describedby={undefined}
            closeLabel={t("spacesCloseExplorer")}
          >
            <DialogHeader>
              <DialogTitle>{t("spacesExplorer")}</DialogTitle>
            </DialogHeader>
            <SpaceNavigation
              roots={roots}
              selectedSpaceId={selectedSpaceId}
              onNavigate={() => setOpen(false)}
            />
          </DialogContent>
        </Dialog>
        {children}
      </div>
    </div>
  );
}
