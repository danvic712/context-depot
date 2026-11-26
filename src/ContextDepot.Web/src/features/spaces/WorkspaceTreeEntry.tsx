import { useId, useState, type CSSProperties } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";
import { RequestFeedback } from "@/components/feedback/RequestFeedback";
import { useDisclosureMotion } from "@/hooks/use-disclosure-motion";
import { paginationDefaults } from "@/lib/pagination";
import type { Space } from "./spaces-api";
import { useWorkspaceChildren } from "./use-space-directory";
import type { useWorkspaceTree } from "./use-workspace-tree";
import { WorkspaceRow, WorkspaceRowSkeleton } from "./WorkspaceRow";

type Tree = ReturnType<typeof useWorkspaceTree>;
const maximumVisualDepth = 3;

function indentStyle(depth: number): CSSProperties {
  return {
    "--workspace-depth": Math.min(depth, maximumVisualDepth),
  } as CSSProperties;
}

export function WorkspaceTreeEntry({
  space,
  tree,
  depth = 0,
}: {
  space: Space;
  tree: Tree;
  depth?: number;
}) {
  const { t } = useTranslation();
  const branchId = useId();
  const pages = tree.branches.get(space.id) ?? 0;
  const expanded = space.subspaceCount > 0 && pages > 0;
  const [visiblePages, setVisiblePages] = useState(pages);
  const branchRef = useDisclosureMotion(expanded, () => setVisiblePages(0));
  if (expanded && visiblePages !== pages) setVisiblePages(pages);
  return (
    <li
      className="workspace-list-entry workspace-tree-entry"
      style={indentStyle(depth)}
    >
      <div
        className="workspace-tree-line"
        data-nested={depth > 0 || undefined}
        data-connected={(depth > 0 && depth <= maximumVisualDepth) || undefined}
      >
        {space.subspaceCount > 0 ? (
          <Button
            type="button"
            variant="ghost"
            size="icon"
            className="workspace-tree-toggle"
            aria-label={t(
              expanded ? "spacesCollapseBranch" : "spacesExpandBranch",
              { name: space.name },
            )}
            aria-expanded={expanded}
            aria-controls={branchId}
            onClick={() =>
              tree.setPages(space.id, expanded ? 0 : visiblePages || 1)
            }
          >
            <span className="workspace-tree-glyph" aria-hidden="true" />
          </Button>
        ) : (
          <span className="workspace-tree-leaf" aria-hidden="true">
            <span className="workspace-tree-glyph" />
          </span>
        )}
        <WorkspaceRow
          space={space}
          directoryPage={tree.directoryPage}
          expanded={expanded}
        />
      </div>
      {space.subspaceCount > 0 && (
        <div
          id={branchId}
          ref={branchRef}
          className="workspace-tree-disclosure"
          inert={!expanded}
          aria-hidden={!expanded}
        >
          <ul
            className="workspace-tree-branch"
            data-guide={depth < maximumVisualDepth || undefined}
          >
            {visiblePages > 0 && (
              <WorkspaceTreePage
                parent={space}
                tree={tree}
                depth={depth + 1}
                page={1}
                pages={visiblePages}
              />
            )}
          </ul>
        </div>
      )}
    </li>
  );
}

function WorkspaceTreePage({
  parent,
  tree,
  depth,
  page,
  pages,
}: {
  parent: Space;
  tree: Tree;
  depth: number;
  page: number;
  pages: number;
}) {
  const { t } = useTranslation();
  const { data, pending, error, retry } = useWorkspaceChildren(
    parent.id,
    page,
    tree.sort,
  );
  const hasMore = data && page < Math.ceil(data.totalCount / data.pageSize);
  const pageSize = paginationDefaults.pageSize;
  const placeholderCount = Math.min(
    pageSize,
    Math.max(1, parent.subspaceCount - (page - 1) * pageSize),
  );
  return (
    <>
      {!data && pending && (
        <li
          aria-busy="true"
          style={indentStyle(depth)}
          className="workspace-tree-entry"
        >
          <span className="sr-only" role="status">
            {t("loading")}
          </span>
          {Array.from({ length: placeholderCount }, (_, index) => (
            <div className="workspace-tree-line" data-nested key={index}>
              <span aria-hidden="true" />
              <WorkspaceRowSkeleton />
            </div>
          ))}
        </li>
      )}
      {error && (
        <li className="workspace-tree-status">
          <RequestFeedback
            title={t("spacesChildrenError")}
            failure={error}
            pending={pending}
            onRetry={retry}
            stale={!!data}
            compact
          />
        </li>
      )}
      {data?.items.map((space) => (
        <WorkspaceTreeEntry
          key={space.id}
          space={space}
          tree={tree}
          depth={depth}
        />
      ))}
      {page === 1 && data?.totalCount === 0 && (
        <li className="workspace-tree-status">{t("spacesNoChildren")}</li>
      )}
      {hasMore &&
        (page < pages ? (
          <WorkspaceTreePage
            parent={parent}
            tree={tree}
            depth={depth}
            page={page + 1}
            pages={pages}
          />
        ) : (
          <li className="workspace-tree-status">
            <Button
              type="button"
              variant="ghost"
              size="sm"
              aria-label={t("spacesMoreChildrenFor", { name: parent.name })}
              onClick={() => tree.setPages(parent.id, page + 1)}
            >
              {t("spacesMoreChildren")}
            </Button>
          </li>
        ))}
    </>
  );
}
