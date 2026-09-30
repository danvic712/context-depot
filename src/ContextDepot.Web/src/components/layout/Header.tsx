import { useTranslation } from "react-i18next";
import { ChevronDownIcon } from "lucide-react";
import { Brand } from "../Brand";
import { Button } from "../ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuLabel,
  DropdownMenuRadioGroup,
  DropdownMenuRadioItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "../ui/dropdown-menu";
import type { Lang } from "@/lib/i18n";
import type { Theme } from "@/features/settings/browser-preferences";
import "@/styles/header.css";

interface Props {
  theme: Theme | null;
  lang: Lang | null;
  onTheme: (value: Theme | null) => void;
  onLanguage: (value: Lang | null) => void;
}

function PreferenceMenu({
  label,
  value,
  options,
  scope,
  onChange,
}: {
  label: string;
  value: string;
  options: readonly { value: string; label: string }[];
  scope: string;
  onChange: (value: string) => void;
}) {
  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          variant="outline"
          aria-label={label}
          title={options.find((option) => option.value === value)?.label}
        >
          {label}
          <ChevronDownIcon data-icon="inline-end" />
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="preference-menu">
        <DropdownMenuGroup>
          <DropdownMenuLabel>{label}</DropdownMenuLabel>
        </DropdownMenuGroup>
        <DropdownMenuRadioGroup value={value} onValueChange={onChange}>
          {options.map((option) => (
            <DropdownMenuRadioItem key={option.value} value={option.value}>
              {option.label}
            </DropdownMenuRadioItem>
          ))}
        </DropdownMenuRadioGroup>
        <DropdownMenuSeparator />
        <p className="preference-scope">{scope}</p>
      </DropdownMenuContent>
    </DropdownMenu>
  );
}

export function Header({ theme, lang, onTheme, onLanguage }: Props) {
  const { t } = useTranslation();
  return (
    <header className="topbar">
      <div className="brand-lockup">
        <Brand />
        <div>
          <strong>ContextDepot</strong>
          <small>{t("tagline")}</small>
        </div>
      </div>
      <div className="top-controls">
        <PreferenceMenu
          label={t("theme")}
          value={theme ?? "default"}
          scope={t("browserOnly")}
          onChange={(value) =>
            onTheme(value === "default" ? null : (value as Theme))
          }
          options={[
            { value: "default", label: t("useDefault") },
            { value: "system", label: t("system") },
            { value: "light", label: t("light") },
            { value: "dark", label: t("dark") },
          ]}
        />
        <PreferenceMenu
          label={t("language")}
          value={lang ?? "default"}
          scope={t("browserOnly")}
          onChange={(value) =>
            onLanguage(value === "default" ? null : (value as Lang))
          }
          options={[
            { value: "default", label: t("useDefault") },
            { value: "zh", label: "中文" },
            { value: "en", label: "English" },
          ]}
        />
      </div>
    </header>
  );
}
