import { useRef } from "react";
import { useTranslation } from "react-i18next";
import { ToggleGroup, ToggleGroupItem } from "@/components/ui/toggle-group";
import { useNavigationIndicatorMotion } from "@/hooks/use-navigation-indicator-motion";
import type { KnowledgeType } from "./knowledge-models";

type SearchType = "all" | KnowledgeType;

export function SearchDialogTypeFilter({
  value,
  onChange,
  counts,
}: {
  value: SearchType;
  onChange: (value: SearchType) => void;
  counts?: Record<SearchType, number>;
}) {
  const { t } = useTranslation();
  const ref = useRef<HTMLDivElement>(null);
  useNavigationIndicatorMotion(ref, value);
  return (
    <ToggleGroup
      ref={ref}
      className="search-dialog-types"
      type="single"
      variant="default"
      size="sm"
      spacing={1}
      value={value}
      onValueChange={(next) => {
        if (next) onChange(next as SearchType);
      }}
      aria-label={t("typeLabel")}
    >
      <span className="search-type-selection" aria-hidden="true" />
      {(["all", "context", "document"] as const).map((type) => (
        <ToggleGroupItem key={type} value={type} data-navigation-item={type}>
          {t(
            type === "all"
              ? "dialogAll"
              : type === "context"
                ? "dialogContexts"
                : "dialogDocuments",
          )}
          {counts && <span className="search-count">{counts[type]}</span>}
        </ToggleGroupItem>
      ))}
    </ToggleGroup>
  );
}
