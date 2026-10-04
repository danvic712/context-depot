import { useEffect, useRef, useState, type ReactNode } from "react";
import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import {
  ChevronLeftIcon,
  ChevronRightIcon,
  FolderIcon,
  LayoutListIcon,
} from "lucide-react";
import { Button } from "@/components/ui/button";
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
    const query =
      roots.page > 1 ? `?${id ? "directoryPage" : "page"}=${roots.page}` : "";
    return `${id ? `/spaces/${id}` : "/spaces"}${query}`;
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
          <div className="space-navigation-pagination">
            <span role="status">
              {t("spacesPage", { page: roots.page, pages })}
            </span>
            <Button
              variant="outline"
              size="icon"
              aria-label={t("spacesPrevious")}
              disabled={roots.page <= 1 || roots.pending}
              onClick={() => roots.changePage(roots.page - 1)}
            >
              <ChevronLeftIcon aria-hidden="true" />
            </Button>
            <Button
              variant="outline"
              size="icon"
              aria-label={t("spacesNext")}
              disabled={roots.page >= pages || roots.pending}
              onClick={() => roots.changePage(roots.page + 1)}
            >
              <ChevronRightIcon aria-hidden="true" />
            </Button>
          </div>
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
