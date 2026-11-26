import { useRef, type ReactNode } from "react";
import { Separator } from "@/components/ui/separator";
import { knowledgeReturnTarget } from "@/features/knowledge/knowledge-navigation";
import { useDisplayPreference } from "@/lib/display-preferences";
import { changeDisplay } from "@/lib/display-transition";
import { Link, useLocation, useMatch } from "react-router";
import { useTranslation } from "react-i18next";
import {
  ArchiveIcon,
  FilesIcon,
  FolderIcon,
  LayoutGridIcon,
  ListIcon,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { CreateWorkspaceDialog } from "@/features/home/CreateWorkspaceDialog";
import { ToggleGroup, ToggleGroupItem } from "@/components/ui/toggle-group";
import { SpaceKnowledge } from "./SpaceKnowledge";
import { ArchivedDocuments } from "./ArchivedDocuments";
import { spaceContentsPath } from "@/features/knowledge/knowledge-routes";
import { SegmentIndicator } from "./SegmentIndicator";
import { SpaceChildren } from "./SpaceChildren";
import type { Space } from "./spaces-api";
import type { SpaceRouteData } from "./space-route-loader";

export function SpaceContents({
  space,
  panel,
  onChildCreated,
  actions,
}: {
  space: Space;
  panel?: SpaceRouteData["panel"];
  onChildCreated: () => void;
  actions?: ReactNode;
}) {
  const { t, i18n } = useTranslation();
  const { id: spaceId, subspaceCount } = space;
  const contentCount = space.contextCount + space.documentCount;
  const number = new Intl.NumberFormat(i18n.resolvedLanguage);
  const formattedCount = number.format(contentCount);
  const location = useLocation();
  const reading = /\/(contexts|documents)\/[^/]+$/.test(location.pathname);
  const basePath = reading
    ? knowledgeReturnTarget(location.state, spaceId).to.split("?")[0]
    : location.pathname;
  const showingChildren = !!useMatch("/spaces/:spaceId/children");
  const view = showingChildren
    ? "children"
    : basePath?.endsWith("/archived")
      ? "archived"
      : "current";
  const [layout, setLayout] = useDisplayPreference("knowledgeLayout");
  const region = useRef<HTMLDivElement>(null);
  return (
    <>
      <div
        className="space-content-toolbar"
        role="group"
        aria-label={t("spacesActions")}
      >
        <nav className="space-content-view" aria-label={t("spacesContentView")}>
          <SegmentIndicator
            value={showingChildren ? "children" : "knowledge"}
            selector='a[aria-current="page"]'
          />
          <Link
            preventScrollReset
            to={spaceContentsPath(spaceId) + location.search}
            aria-current={showingChildren ? undefined : "page"}
          >
            <FilesIcon aria-hidden="true" />
            {t("spacesKnowledgeTab")}
            <span
              className="space-content-count"
              title={t("spacesKnowledgeCount", {
                count: contentCount,
                value: formattedCount,
              })}
            >
              {formattedCount}
            </span>
          </Link>
          <Link
            preventScrollReset
            to={`${spaceContentsPath(spaceId)}/children${location.search}`}
            aria-current={showingChildren ? "page" : undefined}
          >
            <FolderIcon aria-hidden="true" />
            {t("spacesChildrenTab")}
            <span className="space-content-count">
              {number.format(subspaceCount)}
            </span>
          </Link>
        </nav>
        <div className="space-content-actions">
          {view === "current" && (
            <ToggleGroup
              className="space-layout-switch"
              type="single"
              value={layout}
              aria-label={t("spacesDisplayMode")}
            >
              <SegmentIndicator value={layout} />
              <ToggleGroupItem
                value="list"
                onClick={() =>
                  changeDisplay(region.current, () => setLayout("list"))
                }
                aria-label={t("spacesListView")}
                title={t("spacesListView")}
              >
                <ListIcon aria-hidden="true" />
              </ToggleGroupItem>
              <ToggleGroupItem
                value="cards"
                onClick={() =>
                  changeDisplay(region.current, () => setLayout("cards"))
                }
                aria-label={t("spacesCardView")}
                title={t("spacesCardView")}
              >
                <LayoutGridIcon aria-hidden="true" />
              </ToggleGroupItem>
            </ToggleGroup>
          )}
          {(actions || showingChildren) && (
            <>
              {view === "current" && (
                <Separator
                  orientation="vertical"
                  className="space-toolbar-divider"
                />
              )}
              <div className="space-toolbar-commands">
                {view === "current" && (
                  <Button asChild variant="outline" size="sm">
                    <Link
                      preventScrollReset
                      to={spaceContentsPath(spaceId, true) + location.search}
                    >
                      <ArchiveIcon
                        data-icon="inline-start"
                        aria-hidden="true"
                      />
                      {t("spacesArchiveView")}
                    </Link>
                  </Button>
                )}
                {view === "archived" && (
                  <Button asChild variant="outline" size="sm">
                    <Link
                      preventScrollReset
                      to={spaceContentsPath(spaceId) + location.search}
                    >
                      {t("spacesBackToKnowledge")}
                    </Link>
                  </Button>
                )}
                {actions}
                {showingChildren && (
                  <CreateWorkspaceDialog
                    parent={space}
                    onCreated={onChildCreated}
                  />
                )}
              </div>
            </>
          )}
        </div>
      </div>
      <div className="space-content-region" key={view} ref={region}>
        {view === "current" && (
          <SpaceKnowledge
            spaceId={spaceId}
            seed={panel?.view === "knowledge" ? panel.seed : undefined}
          />
        )}
        {view === "archived" && (
          <ArchivedDocuments
            spaceId={spaceId}
            seed={panel?.view === "archive" ? panel.seed : undefined}
          />
        )}
        {view === "children" && (
          <SpaceChildren
            spaceId={spaceId}
            seed={panel?.view === "children" ? panel.seed : undefined}
          />
        )}
      </div>
    </>
  );
}
