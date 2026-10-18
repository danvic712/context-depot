import { useId, useState } from "react";
import { InfoIcon, SearchIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Field, FieldLabel } from "@/components/ui/field";
import { Empty, EmptyDescription } from "@/components/ui/empty";
import { ToggleGroup, ToggleGroupItem } from "@/components/ui/toggle-group";
import {
  Popover,
  PopoverTrigger,
  PopoverContent,
} from "@/components/ui/popover";
import {
  InputGroup,
  InputGroupAddon,
  InputGroupInput,
} from "@/components/ui/input-group";
import { filterKeyWorkspaces, setVisibleKeyGrants } from "./access-key-grants";
import type { KeyWorkspace } from "./settings-api";

export function AccessKeyWorkspacePicker({
  workspaces,
  grants,
  disabled,
  invalid,
  onChange,
}: {
  workspaces: KeyWorkspace[];
  grants: string[];
  disabled: boolean;
  invalid: boolean;
  onChange: (grants: string[]) => void;
}) {
  const { t } = useTranslation();
  const id = useId();
  const [query, setQuery] = useState("");
  const [onlySelected, setOnlySelected] = useState(false);
  const visible = filterKeyWorkspaces(workspaces, query, onlySelected, grants);
  const selected = new Set(grants);
  const availableIds = new Set(workspaces.map((space) => space.id));
  const selectedCount = workspaces.filter((space) =>
    selected.has(space.id),
  ).length;
  const unavailableCount = grants.filter((id) => !availableIds.has(id)).length;
  const allVisibleSelected =
    visible.length > 0 && visible.every((space) => selected.has(space.id));
  return (
    <div className="settings-space-picker">
      <div className="settings-space-picker-toolbar">
        <InputGroup className="settings-space-search">
          <InputGroupAddon>
            <SearchIcon aria-hidden="true" />
          </InputGroupAddon>
          <InputGroupInput
            type="search"
            value={query}
            onChange={(event) => setQuery(event.target.value)}
            onKeyDown={(event) => {
              if (event.key === "Enter" && !event.nativeEvent.isComposing)
                event.preventDefault();
            }}
            placeholder={t("settingsSearchSpacesShort")}
            aria-label={t("settingsFindSpaces")}
            disabled={disabled}
            aria-invalid={invalid && !visible.length}
            aria-describedby={
              invalid
                ? "key-grants-description key-grants-error"
                : "key-grants-description"
            }
          />
        </InputGroup>
        <ToggleGroup
          type="single"
          value={onlySelected ? "selected" : "all"}
          onValueChange={(value) => {
            if (value) setOnlySelected(value === "selected");
          }}
          variant="outline"
          spacing={0}
          disabled={disabled}
          className="settings-space-filter"
          aria-label={t("settingsSpaceFilter")}
        >
          <ToggleGroupItem value="all">
            {t("settingsAllSpacesShort")} <span>{workspaces.length}</span>
          </ToggleGroupItem>
          <ToggleGroupItem value="selected">
            {t("settingsSelectedSpacesShort")} <span>{selectedCount}</span>
          </ToggleGroupItem>
        </ToggleGroup>
        <Button
          type="button"
          variant="ghost"
          size="sm"
          className="settings-space-bulk"
          aria-label={t(
            allVisibleSelected
              ? "settingsDeselectResults"
              : "settingsSelectResults",
          )}
          disabled={disabled || !visible.length}
          onClick={() =>
            onChange(setVisibleKeyGrants(grants, visible, !allVisibleSelected))
          }
        >
          {t(
            allVisibleSelected
              ? "settingsDeselectShownShort"
              : "settingsSelectShownShort",
          )}
        </Button>
      </div>
      <span className="sr-only" role="status">
        {t("settingsSpacesSelected", {
          selected: selectedCount,
          total: workspaces.length,
        })}
        . {t("settingsMatchingSpaces", { count: visible.length })}
      </span>
      {unavailableCount > 0 && (
        <p className="settings-field-hint">
          {t("settingsUnavailableGrants", { count: unavailableCount })}
        </p>
      )}
      <div className="settings-workspace-options">
        {visible.map((workspace, index) => (
          <Field
            key={workspace.id}
            orientation="horizontal"
            className="settings-space-option"
            data-selected={selected.has(workspace.id) || undefined}
            data-disabled={disabled}
            data-invalid={index === 0 && invalid}
          >
            <FieldLabel htmlFor={`${id}-${workspace.id}`}>
              <Checkbox
                id={`${id}-${workspace.id}`}
                checked={selected.has(workspace.id)}
                disabled={disabled}
                aria-invalid={index === 0 && invalid}
                aria-describedby={
                  invalid
                    ? "key-grants-description key-grants-error"
                    : "key-grants-description"
                }
                onCheckedChange={(checked) =>
                  onChange(
                    setVisibleKeyGrants(grants, [workspace], checked === true),
                  )
                }
              />
              <span>
                <strong>{workspace.name}</strong>
                <code className="sr-only">{workspace.path}</code>
              </span>
            </FieldLabel>
            <Popover>
              <PopoverTrigger asChild>
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  disabled={disabled}
                  className="settings-space-info"
                  aria-label={t("settingsSpaceDetailsFor", {
                    name: workspace.name,
                  })}
                >
                  <InfoIcon aria-hidden="true" />
                </Button>
              </PopoverTrigger>
              <PopoverContent
                className="settings-space-details"
                side="top"
                align="end"
                collisionPadding={16}
                aria-label={t("settingsSpaceDetailsFor", {
                  name: workspace.name,
                })}
              >
                <strong>{workspace.name}</strong>
                <code>{workspace.path}</code>
              </PopoverContent>
            </Popover>
          </Field>
        ))}
        {!visible.length && (
          <Empty className="settings-space-picker-empty">
            <EmptyDescription>
              {t(
                onlySelected && !query.trim()
                  ? "settingsNoSelectedSpaces"
                  : "settingsNoMatchingSpaces",
              )}
            </EmptyDescription>
          </Empty>
        )}
      </div>
    </div>
  );
}
