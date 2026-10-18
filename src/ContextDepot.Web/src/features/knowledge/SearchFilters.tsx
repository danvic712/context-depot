import { useState, useSyncExternalStore, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import {
  Collapsible,
  CollapsibleContent,
  CollapsibleTrigger,
} from "@/components/ui/collapsible";
import {
  CheckIcon,
  ChevronDownIcon,
  LayersIcon,
  RotateCcwIcon,
  SlidersHorizontalIcon,
  XIcon,
} from "lucide-react";
import {
  Field,
  FieldDescription,
  FieldGroup,
  FieldLabel,
} from "@/components/ui/field";
import { ToggleGroup, ToggleGroupItem } from "@/components/ui/toggle-group";
import { AppSelect } from "@/components/ui/AppSelect";
import { Button } from "@/components/ui/button";
import { RequestFeedback } from "@/components/feedback/RequestFeedback";
import { Badge } from "@/components/ui/badge";
import { Separator } from "@/components/ui/separator";
import { useFilterDisclosureMotion } from "./use-filter-disclosure-motion";
import { Skeleton } from "@/components/ui/skeleton";
import {
  InputGroup,
  InputGroupAddon,
  InputGroupInput,
  InputGroupButton,
} from "@/components/ui/input-group";
import { SearchIcon } from "lucide-react";
import type { RequestFailure } from "@/lib/request-failure";
import { type SearchWorkspace } from "./search-api";
import { contextKinds } from "./knowledge-models";
import {
  activeSearchFilters,
  matchingSearchSpaces,
} from "./search-filter-state";

const compactQuery = "(max-width: 900px)";
function subscribeCompact(notify: () => void) {
  const media = window.matchMedia(compactQuery);
  media.addEventListener("change", notify);
  return () => media.removeEventListener("change", notify);
}
function isCompact() {
  return window.matchMedia(compactQuery).matches;
}
const typeOptions = [
  { value: "all", label: "dialogAll" },
  { value: "contexts", label: "dialogContexts" },
  { value: "documents", label: "dialogDocuments" },
] as const;

export function SearchFilters({
  params,
  children,
  spaces,
  pending,
  error,
  onRetry,
  onFilter,
  onReset,
}: {
  params: URLSearchParams;
  children?: ReactNode;
  spaces?: SearchWorkspace[];
  pending: boolean;
  error?: RequestFailure;
  onRetry: () => void;
  onFilter: (key: "type" | "kind" | "workspace", value: string) => void;
  onReset: () => void;
}) {
  const { t, i18n } = useTranslation();
  const compact = useSyncExternalStore(
    subscribeCompact,
    isCompact,
    () => false,
  );
  const [expanded, setExpanded] = useState(false);
  const [spaceQuery, setSpaceQuery] = useState("");
  const type = params.get("type") ?? "all";
  const workspace = params.get("workspace");
  const activeCount = activeSearchFilters(params);
  const disclosureRef = useFilterDisclosureMotion(!compact || expanded);
  // A forbidden refresh must not expose the previously loaded workspace names.
  const options =
    error && !error.retryable
      ? []
      : [...(spaces ?? [])].sort((a, b) =>
          a.name.localeCompare(b.name, i18n.resolvedLanguage),
        );
  const unavailableScope =
    !!workspace && !options.some((space) => space.path === workspace);
  const matches = matchingSearchSpaces(options, spaceQuery);
  const visibleSpaces = options.filter(
    (space) => space.path === workspace || matches.includes(space),
  );
  return (
    <Collapsible asChild open={!compact || expanded} onOpenChange={setExpanded}>
      <aside
        className="search-controls-pane"
        aria-label={t("searchConditions")}
      >
        <div className="search-filter-heading">
          <div className="search-filter-title">
            <h2>{t("searchConditions")}</h2>
            {activeCount > 0 && (
              <Badge variant="secondary">{activeCount}</Badge>
            )}
          </div>
          <CollapsibleTrigger asChild>
            <Button variant="ghost" className="search-filter-trigger">
              <SlidersHorizontalIcon
                data-icon="inline-start"
                aria-hidden="true"
              />
              {t("searchFilters")}
              {activeCount > 0 && (
                <Badge variant="secondary">{activeCount}</Badge>
              )}
              <ChevronDownIcon data-icon="inline-end" aria-hidden="true" />
            </Button>
          </CollapsibleTrigger>
          <Button
            type="button"
            variant="ghost"
            size="sm"
            aria-label={t("searchReset")}
            title={t("searchReset")}
            disabled={!activeCount && !spaceQuery.trim()}
            onClick={() => {
              onReset();
              setSpaceQuery("");
              document.getElementById("knowledge-search")?.focus();
            }}
          >
            <RotateCcwIcon data-icon="inline-start" aria-hidden="true" />
            {t("searchResetShort")}
          </Button>
        </div>
        <div className="search-controls-scroll">
          {children}
          <CollapsibleContent
            forceMount
            ref={disclosureRef}
            className="search-filter-content"
            inert={compact && !expanded}
            aria-hidden={compact && !expanded}
          >
            <FieldGroup className="search-filters">
              <Field
                className="search-type-field"
                aria-labelledby="search-type-label"
              >
                <FieldLabel id="search-type-label">
                  {t("searchKnowledgeType")}
                </FieldLabel>
                <ToggleGroup
                  type="single"
                  variant="outline"
                  size="sm"
                  orientation="horizontal"
                  value={type}
                  aria-label={t("searchBatchTypes")}
                  onValueChange={(value) => {
                    if (value) onFilter("type", value);
                  }}
                >
                  {typeOptions.map(({ value, label }) => (
                    <ToggleGroupItem key={value} value={value}>
                      <span className="search-filter-option-label">
                        {t(label)}
                      </span>
                    </ToggleGroupItem>
                  ))}
                </ToggleGroup>
              </Field>
              <Field
                className="search-kind-field"
                data-disabled={type === "documents" || undefined}
              >
                <FieldLabel htmlFor="filter-kind">
                  {t("searchContextKind")}
                </FieldLabel>
                <AppSelect
                  id="filter-kind"
                  label={t("searchContextKind")}
                  value={params.get("kind") ?? "all"}
                  disabled={type === "documents"}
                  onChange={(value) => onFilter("kind", value)}
                  options={[
                    { value: "all", label: t("searchAllKinds") },
                    ...contextKinds.map((kind) => ({
                      value: kind,
                      label: t(`${kind}Kind`),
                    })),
                  ]}
                />
                {type === "documents" && (
                  <FieldDescription>
                    {t("searchContextKindHint")}
                  </FieldDescription>
                )}
              </Field>
              <Separator />
              <Field
                className="search-workspace-field"
                aria-labelledby="search-workspace-label"
              >
                <FieldLabel id="search-workspace-label">
                  {t("workspaceLabel")}
                </FieldLabel>
                {error && (
                  <RequestFeedback
                    title={t("dialogWorkspaceError")}
                    failure={error}
                    description={t("dialogWorkspaceErrorWhy")}
                    pending={pending}
                    stale={!!options.length}
                    onRetry={onRetry}
                    compact
                  />
                )}
                {options.length > 6 && (
                  <>
                    <FieldLabel
                      className="sr-only"
                      htmlFor="search-workspace-query"
                    >
                      {t("searchFindSpace")}
                    </FieldLabel>
                    <InputGroup>
                      <InputGroupInput
                        id="search-workspace-query"
                        value={spaceQuery}
                        onChange={(event) => setSpaceQuery(event.target.value)}
                        placeholder={t("searchFindSpace")}
                        autoComplete="off"
                      />
                      <InputGroupAddon align="inline-start">
                        <SearchIcon aria-hidden="true" />
                      </InputGroupAddon>
                      {spaceQuery && (
                        <InputGroupAddon align="inline-end">
                          <InputGroupButton
                            aria-label={t("searchClearSpaceQuery")}
                            size="icon-sm"
                            onClick={() => {
                              setSpaceQuery("");
                              document
                                .getElementById("search-workspace-query")
                                ?.focus();
                            }}
                          >
                            <XIcon aria-hidden="true" />
                          </InputGroupButton>
                        </InputGroupAddon>
                      )}
                    </InputGroup>
                  </>
                )}
                <ToggleGroup
                  className="search-workspace-options"
                  type="single"
                  orientation="vertical"
                  spacing={1}
                  value={workspace ? `space:${workspace}` : "all"}
                  aria-label={t("workspaceLabel")}
                  onValueChange={(value) => {
                    if (value)
                      onFilter(
                        "workspace",
                        value === "all" ? "all" : value.slice(6),
                      );
                  }}
                >
                  <ToggleGroupItem value="all">
                    <LayersIcon aria-hidden="true" />
                    <span className="search-filter-option-label">
                      {t("allSpaces")}
                    </span>
                    <CheckIcon
                      className="search-space-check"
                      aria-hidden="true"
                    />
                  </ToggleGroupItem>
                  {unavailableScope && (
                    <ToggleGroupItem
                      value={`space:${workspace}`}
                      title={t(
                        pending
                          ? "searchCurrentSpace"
                          : "searchUnavailableSpace",
                      )}
                    >
                      <LayersIcon aria-hidden="true" />
                      <span className="search-filter-option-label">
                        {t(
                          pending
                            ? "searchCurrentSpace"
                            : "searchUnavailableSpace",
                        )}
                      </span>
                      <CheckIcon
                        className="search-space-check"
                        aria-hidden="true"
                      />
                    </ToggleGroupItem>
                  )}
                  {visibleSpaces.map((space) => (
                    <ToggleGroupItem
                      key={space.id}
                      value={`space:${space.path}`}
                      title={space.name}
                    >
                      <LayersIcon aria-hidden="true" />
                      <span className="search-filter-option-label">
                        {space.name}
                      </span>
                      <CheckIcon
                        className="search-space-check"
                        aria-hidden="true"
                      />
                    </ToggleGroupItem>
                  ))}
                </ToggleGroup>
                {!!spaceQuery.trim() && !matches.length && (
                  <p className="search-space-note" role="status">
                    {t("searchNoSpaces")}
                  </p>
                )}
                {pending && !spaces && (
                  <div
                    className="search-workspace-loading"
                    role="status"
                    aria-label={t("loading")}
                  >
                    <Skeleton className="h-8 w-full" />
                    <Skeleton className="h-8 w-4/5" />
                  </div>
                )}
              </Field>
            </FieldGroup>
          </CollapsibleContent>
        </div>
      </aside>
    </Collapsible>
  );
}
