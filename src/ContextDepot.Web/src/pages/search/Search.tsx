import { ItemGroup } from "@/components/ui/item";
import { useAppContext } from "@/hooks/use-app-context";
import { useTranslation } from "react-i18next";
import { SearchIcon } from "lucide-react";
import "@/styles/search.css";
import { Notice, Heading } from "@/components/content/PageElements";
import { AppSelect } from "@/components/ui/AppSelect";
import { Field, FieldGroup, FieldLabel } from "@/components/ui/field";
import { StatePreview } from "@/components/feedback/StatePreview";
import { SearchBox } from "@/features/knowledge/SearchBox";
import { SampleRow } from "@/features/knowledge/KnowledgeRow";
import { sampleSpaces } from "@/features/knowledge/sample-data";

export function Search() {
  const {
    preview,
    state,
    onRetry,
    query,
    params,
    results,
    onSearch,
    onFilter,
  } = useAppContext();
  const { t } = useTranslation();
  const hasCriteria = Boolean(
    query ||
    params.get("type") ||
    params.get("kind") ||
    params.get("workspace"),
  );
  return (
    <>
      <Heading
        kicker={t("searchKicker")}
        title={t("searchTitle")}
        sub={t("searchSub")}
      />
      <SearchBox key={query} value={query} onSearch={onSearch} />
      {preview && (
        <FieldGroup className="filters">
          <Field>
            <FieldLabel htmlFor="filter-type">{t("typeLabel")}</FieldLabel>
            <AppSelect
              id="filter-type"
              label={t("typeLabel")}
              value={params.get("type") ?? "all"}
              onChange={(value) => onFilter("type", value)}
              options={[
                { value: "all", label: t("allTypes") },
                { value: "contexts", label: t("contexts") },
                { value: "documents", label: t("documentsFilter") },
              ]}
            />
          </Field>
          <Field>
            <FieldLabel htmlFor="filter-kind">{t("kindLabel")}</FieldLabel>
            <AppSelect
              id="filter-kind"
              label={t("kindLabel")}
              value={params.get("kind") ?? "all"}
              onChange={(value) => onFilter("kind", value)}
              disabled={params.get("type") === "documents"}
              options={[
                { value: "all", label: t("allKinds") },
                { value: "decision", label: t("decisionKind") },
                { value: "preference", label: t("preferenceKind") },
              ]}
            />
          </Field>
          <Field>
            <FieldLabel htmlFor="filter-space">
              {t("workspaceLabel")}
            </FieldLabel>
            <AppSelect
              id="filter-space"
              label={t("workspaceLabel")}
              value={params.get("workspace") ?? "all"}
              onChange={(value) => onFilter("workspace", value)}
              options={[
                { value: "all", label: t("allSpaces") },
                ...sampleSpaces.map((space) => ({
                  value: space.id,
                  label: space.name,
                })),
              ]}
            />
          </Field>
        </FieldGroup>
      )}
      {preview && state === "degraded" && (
        <StatePreview state={state} onRetry={onRetry} />
      )}
      <section className="results">
        <h2>
          {t("search")}
          {preview &&
            hasCriteria &&
            (state === "success" || state === "degraded") && (
              <small> · {results.length}</small>
            )}
        </h2>
        {preview && state !== "success" && state !== "degraded" ? (
          <StatePreview state={state} onRetry={onRetry} />
        ) : preview && hasCriteria ? (
          results.length ? (
            <ItemGroup className="divide-y border-y">
              {results.map((item) => (
                <SampleRow key={item.id} item={item} />
              ))}
            </ItemGroup>
          ) : (
            <Notice
              icon={SearchIcon}
              title={t("noSampleResults")}
              detail={t("changeSearch")}
            />
          )
        ) : (
          <Notice
            icon={SearchIcon}
            title={query ? t("searchWait") : t("searchStart")}
            detail={
              preview
                ? t("previewSearchHint")
                : query
                  ? t("searchWhy")
                  : t("searchStartWhy")
            }
          />
        )}
      </section>
    </>
  );
}
