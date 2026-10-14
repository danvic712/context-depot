import { useRef, useState, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import {
  ChevronDownIcon,
  SearchIcon,
  SlidersHorizontalIcon,
  XIcon,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  Field,
  FieldGroup,
  FieldLabel,
  FieldDescription,
} from "@/components/ui/field";
import { AppSelect } from "@/components/ui/AppSelect";
import { Input } from "@/components/ui/input";
import {
  InputGroup,
  InputGroupInput,
  InputGroupAddon,
  InputGroupButton,
} from "@/components/ui/input-group";
import { Badge } from "@/components/ui/badge";
import { ToggleGroup, ToggleGroupItem } from "@/components/ui/toggle-group";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Popover,
  PopoverTrigger,
  PopoverContent,
} from "@/components/ui/popover";
import { Spinner } from "@/components/ui/spinner";
import { Separator } from "@/components/ui/separator";
import {
  defaultDirectoryFilters,
  directoryParams,
  maximumDirectoryQueryLength,
  readDirectoryFilters,
  type DirectoryFilters,
} from "./directory-query";

import {
  DirectoryParameter,
  WorkspaceContentStatus,
  WorkspaceContentType,
  WorkspaceSortOrder,
  WorkspaceUpdateWindow,
} from "./directory-enums";

export function SpaceDirectoryFilters({
  action,
  filters,
  pending,
  onApply,
  onReset,
}: {
  action: ReactNode;
  filters: DirectoryFilters;
  pending: boolean;
  onApply: (filters: DirectoryFilters) => void;
  onReset: () => void;
}) {
  const { t } = useTranslation();
  const queryInput = useRef<HTMLInputElement>(null);
  const fromInput = useRef<HTMLInputElement>(null);
  const toInput = useRef<HTMLInputElement>(null);
  const appliedSignature = directoryParams({
    ...filters,
    sort: WorkspaceSortOrder.Activity,
  }).toString();
  const [draft, setDraft] = useState(filters);
  const [expanded, setExpanded] = useState(false);
  const [dateError, setDateError] = useState(false);
  const [previousSignature, setPreviousSignature] = useState(appliedSignature);
  if (previousSignature !== appliedSignature) {
    setPreviousSignature(appliedSignature);
    const applied = readDirectoryFilters(new URLSearchParams(appliedSignature));
    setDraft(applied);
    setDateError(false);
  }
  const contents = [
    { value: WorkspaceContentType.All, label: t("spacesFilterAny") },
    { value: WorkspaceContentType.Contexts, label: t("spacesFilterContexts") },
    {
      value: WorkspaceContentType.Documents,
      label: t("spacesFilterDocuments"),
    },
    { value: WorkspaceContentType.Both, label: t("spacesFilterBoth") },
  ];
  const updates = [
    { value: WorkspaceUpdateWindow.All, label: t("spacesFilterAnyTime") },
    ...(
      [
        WorkspaceUpdateWindow.Week,
        WorkspaceUpdateWindow.Month,
        WorkspaceUpdateWindow.Quarter,
      ] as const
    ).map((value) => ({
      value,
      label: t("spacesFilterDays", { days: value }),
    })),
    { value: WorkspaceUpdateWindow.Custom, label: t("spacesFilterCustom") },
  ];
  const chips = [
    ...(filters.query
      ? [
          {
            key: DirectoryParameter.Keyword,
            label: t("spacesFilterKeyword", { query: filters.query }),
            remove: { ...filters, query: "" },
          },
        ]
      : []),
    ...(filters.content !== WorkspaceContentType.All
      ? [
          {
            key: DirectoryParameter.Content,
            label: contents.find((item) => item.value === filters.content)!
              .label,
            remove: { ...filters, content: WorkspaceContentType.All },
          },
        ]
      : []),
    ...(filters.updated !== WorkspaceUpdateWindow.All
      ? [
          {
            key: DirectoryParameter.Updated,
            label:
              filters.updated === WorkspaceUpdateWindow.Custom
                ? t("spacesFilterRange", {
                    from: filters.from || t("spacesFilterAnyTime"),
                    to: filters.to || t("spacesFilterAnyTime"),
                  })
                : t("spacesFilterDays", { days: filters.updated }),
            remove: {
              ...filters,
              updated: WorkspaceUpdateWindow.All,
              from: "",
              to: "",
            },
          },
        ]
      : []),
  ];
  function applyDraft(next: DirectoryFilters, sort = filters.sort) {
    const invalid =
      next.updated === WorkspaceUpdateWindow.Custom &&
      (!next.from || !next.to || next.from > next.to);
    setDateError(invalid);
    if (invalid) {
      setExpanded(true);
      requestAnimationFrame(() =>
        (!next.from ? fromInput : toInput).current?.focus(),
      );
      return false;
    }
    onApply({ ...next, sort });
    return true;
  }
  function resetDraft() {
    setDraft({ ...defaultDirectoryFilters, sort: filters.sort });
    setDateError(false);
    onReset();
  }
  return (
    <div className="space-directory-filters">
      <div className="space-directory-toolbar">
        <form
          className="space-directory-query-form"
          role="search"
          noValidate
          aria-label={t("spacesFindSpaces")}
          onSubmit={(event) => {
            event.preventDefault();
            applyDraft(draft);
          }}
        >
          <Field data-disabled={pending || undefined}>
            <FieldLabel htmlFor="space-directory-query" className="sr-only">
              {t("spacesFindSpaces")}
            </FieldLabel>
            <InputGroup className="space-directory-input">
              <InputGroupInput
                ref={queryInput}
                id="space-directory-query"
                value={draft.query}
                maxLength={maximumDirectoryQueryLength}
                placeholder={t("spacesFilterPlaceholder")}
                aria-describedby="space-directory-query-help"
                onChange={(event) =>
                  setDraft({ ...draft, query: event.target.value })
                }
              />
              <InputGroupAddon align="inline-start">
                <SearchIcon aria-hidden="true" />
              </InputGroupAddon>
              <InputGroupAddon align="inline-end">
                {draft.query && (
                  <InputGroupButton
                    size="icon-sm"
                    aria-label={t("spacesClearKeyword")}
                    onClick={() => {
                      setDraft({ ...draft, query: "" });
                      applyDraft({ ...filters, query: "" });
                      queryInput.current?.focus();
                    }}
                  >
                    <XIcon aria-hidden="true" />
                  </InputGroupButton>
                )}
                <InputGroupButton
                  type="submit"
                  className="space-directory-submit"
                  variant="default"
                  size="sm"
                  disabled={pending}
                  aria-busy={pending}
                  aria-label={pending ? t("spacesFiltering") : undefined}
                >
                  <span
                    className="space-search-submit-label"
                    data-pending={pending || undefined}
                  >
                    {t("spacesApplyFilters")}
                  </span>
                  <Spinner
                    data-icon="inline-start"
                    className="space-search-progress"
                    data-pending={pending || undefined}
                    aria-hidden="true"
                  />
                </InputGroupButton>
              </InputGroupAddon>
            </InputGroup>
            <FieldDescription
              id="space-directory-query-help"
              className="sr-only"
            >
              {t("spacesFilterHelp")}
            </FieldDescription>
          </Field>
        </form>
        <Field
          orientation="horizontal"
          className="space-directory-status-row"
          data-disabled={pending || undefined}
        >
          <FieldLabel id="space-status-label" className="sr-only">
            {t("spacesFilterStatus")}
          </FieldLabel>
          <ToggleGroup
            className="space-status-switch"
            type="single"
            spacing={0}
            value={draft.status}
            aria-labelledby="space-status-label"
            onValueChange={(value) => {
              if (!value) return;
              const status = value as WorkspaceContentStatus;
              const next = {
                ...draft,
                status,
                content:
                  status === WorkspaceContentStatus.Empty
                    ? WorkspaceContentType.All
                    : draft.content,
              };
              setDraft(next);
              applyDraft(next);
            }}
          >
            {[
              { value: WorkspaceContentStatus.All, label: t("spacesViewAll") },
              {
                value: WorkspaceContentStatus.Populated,
                label: t("spacesViewPopulated"),
              },
              {
                value: WorkspaceContentStatus.Empty,
                label: t("spacesViewEmpty"),
              },
            ].map(({ value, label }) => (
              <ToggleGroupItem key={value} value={value}>
                <span className="space-status-label min-w-0 truncate">
                  {label}
                </span>
              </ToggleGroupItem>
            ))}
          </ToggleGroup>
          <AppSelect
            label={t("spacesSortLabel")}
            value={filters.sort}
            disabled={pending}
            onChange={(value) => applyDraft(draft, value as WorkspaceSortOrder)}
            options={[
              {
                value: WorkspaceSortOrder.Activity,
                label: t("spacesSortUpdated"),
              },
              { value: WorkspaceSortOrder.Name, label: t("spacesSortName") },
              {
                value: WorkspaceSortOrder.Knowledge,
                label: t("spacesSortKnowledge"),
              },
            ]}
          />
        </Field>
        <Popover open={expanded} onOpenChange={setExpanded}>
          <PopoverTrigger asChild>
            <Button
              className="space-filter-trigger"
              type="button"
              variant="ghost"
              aria-expanded={expanded}
              aria-controls="space-directory-conditions"
            >
              <SlidersHorizontalIcon
                data-icon="inline-start"
                aria-hidden="true"
              />
              <span className="space-filter-label min-w-0 truncate">
                {t("spacesFilterConditions")}
              </span>
              {chips.some(
                (chip) => chip.key !== DirectoryParameter.Keyword,
              ) && (
                <Badge variant="secondary">
                  {
                    chips.filter(
                      (chip) => chip.key !== DirectoryParameter.Keyword,
                    ).length
                  }
                </Badge>
              )}
              <ChevronDownIcon
                className="space-filter-chevron"
                data-icon="inline-end"
                aria-hidden="true"
              />
            </Button>
          </PopoverTrigger>
          <PopoverContent
            className="space-directory-condition-popover"
            sideOffset={8}
            align="end"
            collisionPadding={12}
            aria-label={t("spacesFilterConditions")}
          >
            <form
              noValidate
              onSubmit={(event) => {
                event.preventDefault();
                event.stopPropagation();
                if (applyDraft(draft)) setExpanded(false);
              }}
            >
              <FieldGroup
                id="space-directory-conditions"
                className="space-directory-conditions"
                data-expanded={expanded}
              >
                <Field
                  data-disabled={
                    pending ||
                    draft.status === WorkspaceContentStatus.Empty ||
                    undefined
                  }
                >
                  <FieldLabel htmlFor="space-filter-content">
                    {t("spacesFilterContent")}
                  </FieldLabel>
                  <AppSelect
                    id="space-filter-content"
                    label={t("spacesFilterContent")}
                    value={draft.content}
                    options={contents}
                    disabled={
                      pending || draft.status === WorkspaceContentStatus.Empty
                    }
                    onChange={(value) =>
                      setDraft({
                        ...draft,
                        content: value as DirectoryFilters["content"],
                      })
                    }
                  />
                </Field>
                <Field data-disabled={pending || undefined}>
                  <FieldLabel htmlFor="space-filter-updated">
                    {t("spacesFilterUpdated")}
                  </FieldLabel>
                  <AppSelect
                    id="space-filter-updated"
                    label={t("spacesFilterUpdated")}
                    value={draft.updated}
                    disabled={pending}
                    options={updates}
                    onChange={(value) => {
                      setDateError(false);
                      setDraft({
                        ...draft,
                        updated: value as DirectoryFilters["updated"],
                      });
                    }}
                  />
                </Field>
                {draft.updated === WorkspaceUpdateWindow.Custom && (
                  <FieldGroup className="space-directory-date-fields">
                    <Field
                      data-disabled={pending || undefined}
                      data-invalid={dateError || undefined}
                    >
                      <FieldLabel htmlFor="space-filter-from">
                        {t("spacesFilterFrom")}
                      </FieldLabel>
                      <Input
                        ref={fromInput}
                        id="space-filter-from"
                        type="date"
                        disabled={pending}
                        required
                        value={draft.from}
                        max={draft.to || undefined}
                        aria-invalid={dateError || undefined}
                        aria-describedby={
                          dateError ? "space-directory-date-error" : undefined
                        }
                        onChange={(event) =>
                          setDraft({
                            ...draft,
                            from: event.target.value,
                          })
                        }
                      />
                    </Field>
                    <Field
                      data-disabled={pending || undefined}
                      data-invalid={dateError || undefined}
                    >
                      <FieldLabel htmlFor="space-filter-to">
                        {t("spacesFilterTo")}
                      </FieldLabel>
                      <Input
                        ref={toInput}
                        id="space-filter-to"
                        type="date"
                        disabled={pending}
                        required
                        value={draft.to}
                        min={draft.from || undefined}
                        aria-invalid={dateError || undefined}
                        aria-describedby={
                          dateError ? "space-directory-date-error" : undefined
                        }
                        onChange={(event) =>
                          setDraft({ ...draft, to: event.target.value })
                        }
                      />
                    </Field>
                    {dateError && (
                      <p
                        id="space-directory-date-error"
                        role="alert"
                        className="space-directory-date-error"
                      >
                        {t("spacesFilterDateError")}
                      </p>
                    )}
                  </FieldGroup>
                )}
                <div className="space-directory-condition-actions">
                  <Button
                    type="button"
                    variant="ghost"
                    disabled={pending}
                    onClick={resetDraft}
                  >
                    {t("spacesResetFilters")}
                  </Button>
                  <Button type="submit" disabled={pending}>
                    {t("spacesApplyConditions")}
                  </Button>
                </div>
              </FieldGroup>
            </form>
          </PopoverContent>
        </Popover>
        <div className="space-directory-actions">
          <Separator
            orientation="vertical"
            className="space-directory-action-divider"
          />
          {action}
        </div>
      </div>
      {chips.length > 0 && (
        <div
          className="space-filter-chips"
          aria-label={t("spacesAppliedFilters")}
        >
          {chips.map((chip) => (
            <Badge variant="secondary" asChild key={chip.key}>
              <button
                type="button"
                disabled={pending}
                aria-label={t("spacesRemoveFilter", { filter: chip.label })}
                onClick={() => onApply(chip.remove)}
              >
                {chip.label}
                <XIcon aria-hidden="true" />
              </button>
            </Badge>
          ))}
          <Button
            type="button"
            variant="ghost"
            disabled={pending}
            onClick={resetDraft}
          >
            {t("spacesResetFilters")}
          </Button>
        </div>
      )}
    </div>
  );
}

export function SpaceDirectoryFiltersSkeleton({
  filters = defaultDirectoryFilters,
}: {
  filters?: DirectoryFilters;
}) {
  const chipCount =
    Number(Boolean(filters.query)) +
    Number(filters.content !== WorkspaceContentType.All) +
    Number(filters.updated !== WorkspaceUpdateWindow.All);
  return (
    <div className="space-directory-filters" aria-hidden="true">
      <div className="space-directory-toolbar">
        <div className="space-directory-query-form">
          <Skeleton className="space-directory-input" />
        </div>
        <div className="space-directory-status-row">
          <div className="space-status-switch flex">
            {Array.from({ length: 3 }, (_, index) => (
              <Skeleton className="space-status-item-skeleton" key={index} />
            ))}
          </div>
          <div data-slot="select-trigger">
            <Skeleton className="h-4 w-28" />
          </div>
        </div>
        <Skeleton className="space-filter-trigger space-filter-trigger-skeleton" />
        <div className="space-directory-actions">
          <Skeleton className="spaces-create-skeleton" />
        </div>
      </div>
      {chipCount > 0 && (
        <div className="space-filter-chips">
          {Array.from({ length: chipCount + 1 }, (_, index) => (
            <Skeleton className="h-11 w-24" key={index} />
          ))}
        </div>
      )}
    </div>
  );
}
