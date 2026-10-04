import { CheckIcon, MonitorIcon, MoonIcon, SunIcon } from "lucide-react";
import { useEffect, useRef } from "react";
import { useTranslation } from "react-i18next";
import { ToggleGroup, ToggleGroupItem } from "@/components/ui/toggle-group";
import type { Theme } from "./browser-preferences";
import "@/styles/theme-options.css";

const options = [
  { value: "light", Icon: SunIcon },
  { value: "dark", Icon: MoonIcon },
  { value: "system", Icon: MonitorIcon },
] as const;

export function ThemeOptions({
  value,
  pending,
  onChange,
}: {
  value: Theme;
  pending: boolean;
  onChange: (theme: Theme) => void;
}) {
  const { t } = useTranslation();
  const groupRef = useRef<HTMLDivElement>(null);
  const focusAfterSave = useRef<HTMLButtonElement | null>(null);
  useEffect(() => {
    if (pending || !focusAfterSave.current) return;
    if (document.activeElement === document.body)
      focusAfterSave.current.focus({ preventScroll: true });
    focusAfterSave.current = null;
  }, [pending]);
  return (
    <ToggleGroup
      ref={groupRef}
      type="single"
      className="settings-theme-options"
      value={value}
      disabled={pending}
      aria-label={t("theme")}
      aria-busy={pending}
      onValueChange={(next) => {
        if (next === "light" || next === "dark" || next === "system") {
          const focused = document.activeElement;
          if (
            focused instanceof HTMLButtonElement &&
            groupRef.current?.contains(focused)
          )
            focusAfterSave.current = focused;
          onChange(next);
        }
      }}
    >
      {options.map(({ value: theme, Icon }) => (
        <ToggleGroupItem key={theme} value={theme} aria-label={t(theme)}>
          <span
            className="theme-preview"
            data-preview-theme={theme}
            aria-hidden="true"
          >
            <span className="theme-preview-rail" />
            <span className="theme-preview-content">
              <span />
              <span />
              <span />
            </span>
          </span>
          <span className="theme-option-label">
            <Icon aria-hidden="true" />
            <span>{t(theme)}</span>
            {value === theme && (
              <CheckIcon className="theme-option-check" aria-hidden="true" />
            )}
          </span>
        </ToggleGroupItem>
      ))}
    </ToggleGroup>
  );
}
