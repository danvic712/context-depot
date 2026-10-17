import { useTranslation } from "react-i18next";
import { ArrowDownWideNarrowIcon, ChevronDownIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { WorkspaceSortOrder } from "./directory-enums";

export function SpaceSortMenu({
  value,
  disabled,
  onValueChange,
}: {
  value: WorkspaceSortOrder;
  disabled?: boolean;
  onValueChange: (value: WorkspaceSortOrder) => void;
}) {
  const { t } = useTranslation();
  const options = [
    { value: WorkspaceSortOrder.Activity, label: t("spacesSortUpdated") },
    { value: WorkspaceSortOrder.Name, label: t("spacesSortName") },
    { value: WorkspaceSortOrder.Knowledge, label: t("spacesSortKnowledge") },
  ];
  const selected = options.find((option) => option.value === value)!;
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          variant="outline"
          className="space-directory-menu-trigger space-sort-trigger"
          disabled={disabled}
          aria-label={t("spacesSortSelected", { sort: selected.label })}
        >
          <ArrowDownWideNarrowIcon
            data-icon="inline-start"
            aria-hidden="true"
          />
          <span>{selected.label}</span>
          <ChevronDownIcon
            className="space-filter-chevron"
            data-icon="inline-end"
            aria-hidden="true"
          />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent
        className="space-sort-menu"
        align="end"
        sideOffset={8}
        collisionPadding={12}
        aria-label={t("spacesSortLabel")}
      >
        <DropdownMenuGroup>
          <DropdownMenuLabel>{t("spacesSortLabel")}</DropdownMenuLabel>
          <DropdownMenuRadioGroup
            value={value}
            onValueChange={(next) => {
              const option = options.find((item) => item.value === next);
              if (option) onValueChange(option.value);
            }}
          >
            {options.map((option) => (
              <DropdownMenuRadioItem
                key={option.value}
                value={option.value}
                indicatorPosition="end"
                className="cursor-pointer"
              >
                {option.label}
              </DropdownMenuRadioItem>
            ))}
          </DropdownMenuRadioGroup>
        </DropdownMenuGroup>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
