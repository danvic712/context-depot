import { useRef, type ReactNode } from "react";
import { Separator } from "@/components/ui/separator";
import { knowledgeReturnTarget } from "@/features/knowledge/knowledge-navigation";
import { useDisplayPreference } from "@/lib/display-preferences";
import { changeDisplay } from "@/lib/display-transition";
import { Link, useLocation } from "react-router";
import { useTranslation } from "react-i18next";
import { ArchiveIcon, FilesIcon, LayoutGridIcon, ListIcon } from "lucide-react";
import { ToggleGroup, ToggleGroupItem } from "@/components/ui/toggle-group";
import { SpaceKnowledge } from "./SpaceKnowledge";
import { ArchivedDocuments } from "./ArchivedDocuments";
import { spaceContentsPath } from "@/features/knowledge/knowledge-routes";
import { SegmentIndicator } from "./SegmentIndicator";

export function SpaceContents({
  spaceId,
  contentCount,
  actions,
}: {
  spaceId: string;
  contentCount: number;
  actions?: ReactNode;
}) {
  const { t, i18n } = useTranslation();
  const formattedCount = new Intl.NumberFormat(i18n.resolvedLanguage).format(
    contentCount,
  );
  const location = useLocation();
  const reading = /\/(contexts|documents)\/[^/]+$/.test(location.pathname);
  const basePath = reading
    ? knowledgeReturnTarget(location.state, spaceId).to.split("?")[0]
    : location.pathname;
  const view = basePath?.endsWith("/archived") ? "archived" : "current";
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
          <SegmentIndicator value={view} selector='a[aria-current="page"]' />
          <Link
            preventScrollReset
            viewTransition
            to={spaceContentsPath(spaceId) + location.search}
            aria-current={view === "current" ? "page" : undefined}
          >
            <FilesIcon aria-hidden="true" />
            {t("spacesCurrentContent")}
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
            viewTransition
            to={spaceContentsPath(spaceId, true) + location.search}
            aria-current={view === "archived" ? "page" : undefined}
          >
            <ArchiveIcon aria-hidden="true" />
            {t("spacesArchiveView")}
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
          {actions && (
            <>
              {view === "current" && (
                <Separator
                  orientation="vertical"
                  className="space-toolbar-divider"
                />
              )}
              <div className="space-toolbar-commands">{actions}</div>
            </>
          )}
        </div>
      </div>
      <div className="space-content-region" key={view} ref={region}>
        {view === "current" ? (
          <SpaceKnowledge spaceId={spaceId} />
        ) : (
          <ArchivedDocuments spaceId={spaceId} />
        )}
      </div>
    </>
  );
}
