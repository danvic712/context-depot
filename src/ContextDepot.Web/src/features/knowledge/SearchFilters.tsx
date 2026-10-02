import { useTranslation } from "react-i18next";
import { Field, FieldGroup, FieldLabel } from "@/components/ui/field";
import { ToggleGroup, ToggleGroupItem } from "@/components/ui/toggle-group";
import { AppSelect } from "@/components/ui/AppSelect";
import { Button } from "@/components/ui/button";
import {
  contextKinds,
  type SearchWorkspace,
  type SearchHit,
} from "./search-api";

export function SearchFilters({
  params,
  hits,
  spaces,
  pending,
  onFilter,
  onReset,
}: {
  params: URLSearchParams;
  hits?: SearchHit[];
  spaces?: SearchWorkspace[];
  pending: boolean;
  onFilter: (key: "type" | "kind" | "workspace", value: string) => void;
  onReset: () => void;
}) {
  const { t } = useTranslation();
  const type = params.get("type") ?? "all";
  const workspace = params.get("workspace");
  const options = spaces ?? [];
  return (
    <FieldGroup className="search-filters">
      <Field className="search-type-field">
        <FieldLabel className="sr-only">{t("typeLabel")}</FieldLabel>
        <ToggleGroup
          type="single"
          variant="outline"
          value={type}
          aria-label={t("searchBatchTypes")}
          onValueChange={(value) => {
            if (value) onFilter("type", value);
          }}
        >
          {(["all", "contexts", "documents"] as const).map((value) => (
            <ToggleGroupItem key={value} value={value}>
              {t(
                value === "all"
                  ? "dialogAll"
                  : value === "contexts"
                    ? "dialogContexts"
                    : "dialogDocuments",
              )}
              {hits && (
                <span className="search-count">
                  {value === "all"
                    ? hits.length
                    : hits.filter(
                        (hit) =>
                          hit.type ===
                          (value === "contexts" ? "context" : "document"),
                      ).length}
                </span>
              )}
            </ToggleGroupItem>
          ))}
        </ToggleGroup>
      </Field>
      <Field className="search-kind-field">
        <FieldLabel htmlFor="filter-kind" className="sr-only">
          {t("kindLabel")}
        </FieldLabel>
        <AppSelect
          id="filter-kind"
          label={t("kindLabel")}
          value={params.get("kind") ?? "all"}
          disabled={type === "documents"}
          onChange={(value) => onFilter("kind", value)}
          options={[
            { value: "all", label: t("allKinds") },
            ...contextKinds.map((kind) => ({
              value: kind,
              label: t(`${kind}Kind`),
            })),
          ]}
        />
      </Field>
      <Field className="search-workspace-field">
        <FieldLabel htmlFor="filter-workspace" className="sr-only">
          {t("workspaceLabel")}
        </FieldLabel>
        <AppSelect
          id="filter-workspace"
          label={t("workspaceLabel")}
          value={workspace ? `space:${workspace}` : "all"}
          disabled={pending && !spaces && !workspace}
          onChange={(value) =>
            onFilter("workspace", value === "all" ? "all" : value.slice(6))
          }
          options={[
            { value: "all", label: t("allSpaces") },
            ...(workspace && !options.some((space) => space.path === workspace)
              ? [{ value: `space:${workspace}`, label: workspace }]
              : []),
            ...options.map((space) => ({
              value: `space:${space.path}`,
              label: space.path,
            })),
          ]}
        />
      </Field>
      <Button
        type="button"
        variant="ghost"
        onClick={onReset}
        disabled={!params.has("type") && !params.has("kind") && !workspace}
      >
        {t("searchReset")}
      </Button>
    </FieldGroup>
  );
}
