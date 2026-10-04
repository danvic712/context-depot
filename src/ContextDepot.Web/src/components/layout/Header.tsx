import { useTranslation } from "react-i18next";
import {
  LanguagesIcon,
  SearchIcon,
  CaseSensitiveIcon,
  ChevronDownIcon,
  MonitorIcon,
  MoonIcon,
  SunIcon,
  LoaderCircleIcon,
  type LucideIcon,
} from "lucide-react";
import { Separator } from "../ui/separator";
import { Button } from "../ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuTrigger,
} from "../ui/dropdown-menu";
import type { Lang } from "@/lib/i18n";
import type { Theme } from "@/features/settings/browser-preferences";
import { SearchShortcut } from "@/components/content/SearchShortcut";
import "@/styles/header.css";

interface Props {
  theme: Theme;
  lang: Lang;
  languagePending: boolean;
  appearancePending?: boolean;
  onTheme: (value: Theme) => void;
  onLanguage: (value: Lang) => void;
  onSearch?: () => void;
}

function PreferenceMenu({
  label,
  Icon,
  value,
  options,
  onChange,
  pending = false,
  disabled = false,
}: {
  label: string;
  Icon: LucideIcon;
  value: string;
  options: readonly {
    value: string;
    label: string;
    Icon?: LucideIcon;
    lang?: string;
  }[];
  onChange: (value: string) => void;
  pending?: boolean;
  disabled?: boolean;
}) {
  const selected = options.find((option) => option.value === value);
  const { t } = useTranslation();
  const description = `${label}: ${pending ? t("loading") : (selected?.label ?? label)}`;
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          variant="outline"
          className="preference-trigger cursor-pointer"
          aria-label={description}
          title={description}
          disabled={pending || disabled}
          aria-busy={pending}
        >
          {pending ? (
            <LoaderCircleIcon
              className="animate-spin"
              data-icon="inline-start"
              aria-hidden="true"
            />
          ) : (
            <Icon data-icon="inline-start" aria-hidden="true" />
          )}
          <span className="preference-value" lang={selected?.lang}>
            {pending ? t("loading") : selected?.label}
          </span>
          <ChevronDownIcon
            className="preference-chevron"
            data-icon="inline-end"
            aria-hidden="true"
          />
          {pending && (
            <span className="sr-only" role="status">
              {t("loading")}
            </span>
          )}
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="preference-menu">
        <DropdownMenuGroup>
          <DropdownMenuLabel>{label}</DropdownMenuLabel>
          <DropdownMenuRadioGroup value={value} onValueChange={onChange}>
            {options.map((option) => (
              <DropdownMenuRadioItem
                key={option.value}
                value={option.value}
                indicatorPosition="end"
                className="preference-option cursor-pointer"
              >
                {option.Icon && <option.Icon aria-hidden="true" />}
                <span lang={option.lang}>{option.label}</span>
              </DropdownMenuRadioItem>
            ))}
          </DropdownMenuRadioGroup>
        </DropdownMenuGroup>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

const themeIcons = {
  system: MonitorIcon,
  light: SunIcon,
  dark: MoonIcon,
};

export function Header({
  theme,
  lang,
  languagePending,
  appearancePending = false,
  onTheme,
  onLanguage,
  onSearch,
}: Props) {
  const { t } = useTranslation();
  return (
    <header className="topbar">
      <div className="brand-lockup">
        <strong>ContextDepot</strong>
        <Separator orientation="vertical" className="brand-divider" />
        <small>{t("tagline")}</small>
      </div>
      <div className="top-controls">
        {onSearch && (
          <Button
            variant="outline"
            className="header-search cursor-pointer"
            aria-label={t("dialogSearchTitle")}
            title={t("dialogSearchShortcut")}
            onClick={onSearch}
          >
            <SearchIcon data-icon="inline-start" aria-hidden="true" />
            <span className="header-search-label">{t("searchButton")}</span>
            <SearchShortcut className="header-search-shortcut" />
          </Button>
        )}
        <PreferenceMenu
          label={t("theme")}
          Icon={themeIcons[theme]}
          value={theme}
          disabled={appearancePending}
          pending={appearancePending && !languagePending}
          onChange={(value) => onTheme(value as Theme)}
          options={[
            { value: "system", label: t("system"), Icon: MonitorIcon },
            { value: "light", label: t("light"), Icon: SunIcon },
            { value: "dark", label: t("dark"), Icon: MoonIcon },
          ]}
        />
        <PreferenceMenu
          label={t("language")}
          Icon={LanguagesIcon}
          value={lang}
          onChange={(value) => onLanguage(value as Lang)}
          pending={languagePending}
          disabled={appearancePending}
          options={[
            { value: "zh", label: "中文", lang: "zh-CN", Icon: LanguagesIcon },
            {
              value: "en",
              label: "English",
              lang: "en",
              Icon: CaseSensitiveIcon,
            },
          ]}
        />
      </div>
    </header>
  );
}
