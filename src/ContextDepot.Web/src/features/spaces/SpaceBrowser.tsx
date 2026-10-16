import { useEffect, useRef, useState } from "react";
import { Link, useSearchParams } from "react-router";
import { useTranslation } from "react-i18next";
import {
  ArrowUpRightIcon,
  CheckIcon,
  ChevronDownIcon,
  FolderIcon,
  LayoutListIcon,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
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

export function SpaceNavigation({
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
  const [pagination, setPagination] = useState<{
    totalCount: number;
    pageSize: number;
  }>();
  if (
    data &&
    (pagination?.totalCount !== data.totalCount ||
      pagination.pageSize !== data.pageSize)
  ) {
    setPagination({ totalCount: data.totalCount, pageSize: data.pageSize });
  }
  const pageInfo =
    data ?? (roots.pending && !roots.error ? pagination : undefined);
  const [pageDirection, setPageDirection] = useState<"next" | "previous">();
  const pages = Math.max(
    1,
    Math.ceil((pageInfo?.totalCount ?? 0) / (pageInfo?.pageSize ?? 12)),
  );
  const home = useRef<HTMLAnchorElement>(null);
  const scrollArea = useRef<HTMLDivElement>(null);
  const previousPage = useRef<number | undefined>(undefined);
  const loadedPage = data?.page;
  useEffect(() => {
    if (loadedPage === undefined) return;
    if (scrollArea.current) scrollArea.current.scrollTop = 0;
    if (
      previousPage.current !== undefined &&
      previousPage.current !== loadedPage &&
      (document.activeElement === document.body ||
        document.activeElement ===
          scrollArea.current?.closest('[role="dialog"]')) &&
      home.current?.getClientRects().length
    ) {
      const first = scrollArea.current?.querySelector<HTMLAnchorElement>(
        ".space-navigation-link",
      );
      (first ?? home.current).focus({ preventScroll: true });
    }
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
  function changePage(page: number) {
    setPageDirection(page > roots.page ? "next" : "previous");
    roots.changePage(page);
  }
  return (
    <nav className="space-navigation" aria-label={t("spacesExplorer")}>
      <div
        ref={scrollArea}
        className="space-navigation-group"
        aria-busy={roots.pending}
      >
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
          <div className="space-navigation-loading">
            <span className="sr-only" role="status">
              {t("loading")}
            </span>
            {Array.from({ length: pageInfo?.pageSize ?? 6 }, (_, index) => (
              <Skeleton key={index} className="space-navigation-skeleton" />
            ))}
          </div>
        )}
        {data && (
          <ul
            key={data.page}
            className="space-navigation-list"
            data-direction={pageDirection}
          >
            {data.items.map((space) => (
              <li key={space.id}>
                <Link
                  className="space-navigation-link"
                  to={target(space.id)}
                  viewTransition
                  onClick={onNavigate}
                  title={space.path}
                  aria-current={
                    space.id === selectedSpaceId ? "page" : undefined
                  }
                >
                  <span className="space-navigation-icon">
                    <FolderIcon aria-hidden="true" />
                  </span>
                  <span className="space-navigation-copy">
                    <strong title={space.name}>{space.name}</strong>
                    {space.description && (
                      <span className="space-navigation-description">
                        {space.description}
                      </span>
                    )}
                  </span>
                  {space.id === selectedSpaceId && (
                    <Badge variant="secondary">
                      <CheckIcon aria-hidden="true" />
                      {t("spacesCurrentSpace")}
                    </Badge>
                  )}
                </Link>
              </li>
            ))}
          </ul>
        )}
        {data?.totalCount === 0 && (
          <p className="space-navigation-note">{t("spacesExplorerEmpty")}</p>
        )}
      </div>
      <div className="space-navigation-footer">
        <Button variant="ghost" size="sm" asChild>
          <Link
            ref={home}
            className="space-navigation-home"
            to={target()}
            onClick={onNavigate}
          >
            <LayoutListIcon aria-hidden="true" />
            {t("spacesAllSpaces")}
            {pageInfo && (
              <span className="space-navigation-count">
                {new Intl.NumberFormat(i18n.resolvedLanguage).format(
                  pageInfo.totalCount,
                )}
              </span>
            )}
            <ArrowUpRightIcon aria-hidden="true" />
          </Link>
        </Button>
        {pageInfo && pages > 1 && (
          <PaginationBar
            compact
            page={roots.page}
            pageSize={pageInfo.pageSize}
            totalCount={pageInfo.totalCount}
            ariaLabel={t("spacesPagination")}
            onPageChange={changePage}
            disabled={roots.pending}
          />
        )}
      </div>
    </nav>
  );
}

export function SpaceSwitcher({
  roots,
  selectedSpaceId,
}: {
  roots: Directory;
  selectedSpaceId?: string;
}) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const content = useRef<HTMLDivElement>(null);
  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogTrigger asChild>
        <Button className="space-browser-trigger" variant="outline" size="sm">
          <FolderIcon aria-hidden="true" />
          {t("spacesSwitchSpace")}
          <ChevronDownIcon aria-hidden="true" />
        </Button>
      </DialogTrigger>
      <DialogContent
        ref={content}
        className="space-browser-dialog"
        overlayClassName="space-explorer-overlay"
        aria-describedby={undefined}
        closeLabel={t("spacesCloseExplorer")}
        onOpenAutoFocus={(event) => {
          const initial = content.current?.querySelector<HTMLElement>(
            '.space-navigation-link[aria-current="page"], .space-navigation-link',
          );
          const selected = content.current?.querySelector<HTMLElement>(
            '.space-navigation-link[aria-current="page"]',
          );
          if (selected || initial) {
            event.preventDefault();
            (selected ?? initial)?.focus({ preventScroll: true });
          }
        }}
      >
        <DialogHeader className="space-switcher-heading">
          <DialogTitle>{t("spacesExplorer")}</DialogTitle>
        </DialogHeader>
        <SpaceNavigation
          roots={roots}
          selectedSpaceId={selectedSpaceId}
          onNavigate={() => setOpen(false)}
        />
      </DialogContent>
    </Dialog>
  );
}
