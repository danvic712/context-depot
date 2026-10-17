import { KnowledgeOpenLink } from "@/features/knowledge/KnowledgeOpenLink";
import {
  RovingFocusGroup,
  RovingFocusGroupItem,
} from "@radix-ui/react-roving-focus";
import { ChevronRightIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { Skeleton } from "@/components/ui/skeleton";
import { type KnowledgeSummary } from "@/features/knowledge/knowledge-models";
import {
  knowledgeTypes,
  knowledgeKindLabels,
} from "@/features/knowledge/knowledge-types";
import { knowledgeKey } from "@/features/knowledge/knowledge-models";
import configuredI18n from "@/lib/i18n";

export function SpaceKnowledgeColumns({
  loading = false,
}: {
  loading?: boolean;
}) {
  const { t } = useTranslation(undefined, {
    i18n: configuredI18n,
    useSuspense: false,
  });
  return (
    <div className="space-knowledge-columns" aria-hidden="true">
      {loading ? (
        <>
          <Skeleton className="h-3 w-12" />
          <Skeleton className="h-3 w-10" />
          <Skeleton className="h-3 w-16" />
        </>
      ) : (
        <>
          <span>{t("spacesColumnTitle")}</span>
          <span>{t("spacesColumnType")}</span>
          <span>{t("spacesActivity")}</span>
        </>
      )}
    </div>
  );
}

export function SpaceKnowledgeList({
  items,
  layout = "list",
  getHref,
  getState,
}: {
  items: KnowledgeSummary[];
  layout?: "list" | "cards";
  getHref: (item: KnowledgeSummary) => string;
  getState: (item: KnowledgeSummary) => unknown;
}) {
  const { t, i18n } = useTranslation();
  const displayDate = new Intl.DateTimeFormat(i18n.resolvedLanguage, {
    dateStyle: "medium",
  });
  const fullDate = new Intl.DateTimeFormat(i18n.resolvedLanguage, {
    dateStyle: "full",
    timeStyle: "short",
  });
  return (
    <RovingFocusGroup
      asChild
      orientation={layout === "list" ? "vertical" : undefined}
      preventScrollOnEntryFocus
    >
      <ul className="space-knowledge-list" data-layout={layout}>
        {items.map((item) => {
          const key = knowledgeKey(item);
          const Icon = knowledgeTypes[item.type].icon;
          const date = new Date(item.updatedAt);
          return (
            <li key={key}>
              <RovingFocusGroupItem asChild tabStopId={key}>
                <KnowledgeOpenLink
                  item={{
                    type: item.type,
                    id: item.id,
                    title: item.title,
                    spaceId: item.workspace.id,
                  }}
                  className="space-knowledge-row"
                  data-key={key}
                  data-type={item.type}
                  to={getHref(item)}
                  state={getState(item)}
                  preventScrollReset
                >
                  <span className="space-knowledge-icon">
                    <Icon aria-hidden="true" />
                  </span>
                  <span className="space-knowledge-title" title={item.title}>
                    {item.title}
                  </span>
                  <span className="space-knowledge-type">
                    <span>{t(knowledgeTypes[item.type].label)}</span>
                    {item.kind && (
                      <span>{t(knowledgeKindLabels[item.kind])}</span>
                    )}
                    {item.indexStatus && item.indexStatus !== "indexed" && (
                      <span className="space-knowledge-state">
                        {t(
                          item.indexStatus === "pending"
                            ? "homeIndexPending"
                            : "homeIndexFailed",
                        )}
                      </span>
                    )}
                  </span>
                  <time
                    className="space-knowledge-date"
                    dateTime={item.updatedAt}
                    title={fullDate.format(date)}
                  >
                    {displayDate.format(date)}
                  </time>
                  <ChevronRightIcon
                    className="space-knowledge-arrow"
                    aria-hidden="true"
                  />
                </KnowledgeOpenLink>
              </RovingFocusGroupItem>
            </li>
          );
        })}
      </ul>
    </RovingFocusGroup>
  );
}

export function SpaceKnowledgeSkeleton({
  layout = "list",
  count = 6,
}: {
  layout?: "list" | "cards";
  count?: number;
}) {
  return (
    <div
      className="space-knowledge-panel"
      data-layout={layout}
      aria-hidden="true"
    >
      <SpaceKnowledgeColumns loading />
      <div className="space-knowledge-list" data-layout={layout}>
        {Array.from({ length: count }, (_, index) => (
          <div className="space-knowledge-row" key={index}>
            <Skeleton className="space-knowledge-icon" />
            <Skeleton className="space-knowledge-title h-4 w-3/4" />
            <Skeleton className="space-knowledge-type h-3 w-16" />
            <Skeleton className="space-knowledge-date h-3 w-20" />
            <Skeleton className="space-knowledge-arrow h-3" />
          </div>
        ))}
      </div>
    </div>
  );
}
