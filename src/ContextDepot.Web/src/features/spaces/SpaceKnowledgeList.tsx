import { ChevronRightIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { Skeleton } from "@/components/ui/skeleton";
import type { KnowledgeSummary } from "@/features/home/home-api";
import {
  knowledgeTypes,
  knowledgeKindLabels,
} from "@/features/knowledge/knowledge-types";
import { hitKey } from "@/features/knowledge/search-api";

export function SpaceKnowledgeList({
  items,
  selected,
  layout = "list",
  onSelect,
}: {
  items: KnowledgeSummary[];
  selected?: string;
  layout?: "list" | "cards";
  onSelect: (item: KnowledgeSummary) => void;
}) {
  const { t, i18n } = useTranslation();
  return (
    <ul
      className="space-knowledge-list"
      data-layout={layout}
      onKeyDown={(event) => {
        const rows = Array.from(
          event.currentTarget.querySelectorAll<HTMLButtonElement>(
            ".space-knowledge-row",
          ),
        );
        const index = rows.indexOf(document.activeElement as HTMLButtonElement);
        if (index < 0) return;
        const columns =
          layout === "cards"
            ? getComputedStyle(event.currentTarget).gridTemplateColumns.split(
                /\s+/,
              ).length
            : 1;
        const offset =
          event.key === "ArrowDown"
            ? columns
            : event.key === "ArrowUp"
              ? -columns
              : layout === "cards" && event.key === "ArrowRight"
                ? 1
                : layout === "cards" && event.key === "ArrowLeft"
                  ? -1
                  : undefined;
        const next =
          offset !== undefined
            ? Math.max(0, Math.min(index + offset, rows.length - 1))
            : event.key === "Home"
              ? 0
              : event.key === "End"
                ? rows.length - 1
                : undefined;
        if (next !== undefined) {
          event.preventDefault();
          rows[next]?.focus();
        }
      }}
    >
      {items.map((item) => {
        const key = hitKey(item);
        const Icon = knowledgeTypes[item.type].icon;
        const date = new Date(item.updatedAt);
        return (
          <li key={key}>
            <button
              className="space-knowledge-row"
              type="button"
              data-key={key}
              aria-pressed={key === selected}
              onClick={() => onSelect(item)}
            >
              <span className="space-knowledge-icon">
                <Icon aria-hidden="true" />
              </span>
              <span className="space-knowledge-title" title={item.title}>
                {item.title}
              </span>
              <span className="space-knowledge-type">
                <span>{t(knowledgeTypes[item.type].label)}</span>
                {item.kind && <span>{t(knowledgeKindLabels[item.kind])}</span>}
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
                title={new Intl.DateTimeFormat(i18n.resolvedLanguage, {
                  dateStyle: "full",
                  timeStyle: "short",
                }).format(date)}
              >
                {new Intl.DateTimeFormat(i18n.resolvedLanguage, {
                  dateStyle: "medium",
                }).format(date)}
              </time>
              <ChevronRightIcon
                className="space-knowledge-arrow"
                aria-hidden="true"
              />
            </button>
          </li>
        );
      })}
    </ul>
  );
}

export function SpaceKnowledgeSkeleton({
  layout = "list",
}: {
  layout?: "list" | "cards";
}) {
  return (
    <div
      className="space-knowledge-panel"
      data-layout={layout}
      aria-hidden="true"
    >
      <div className="space-knowledge-list" data-layout={layout}>
        {Array.from({ length: 4 }, (_, index) => (
          <div className="space-knowledge-row" key={index}>
            <Skeleton className="space-knowledge-icon" />
            <Skeleton className="space-knowledge-title h-4 w-3/4" />
            <Skeleton className="space-knowledge-type h-3 w-16" />
            <Skeleton className="space-knowledge-date h-3 w-20" />
          </div>
        ))}
      </div>
    </div>
  );
}
