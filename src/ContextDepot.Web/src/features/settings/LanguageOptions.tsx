import { useEffect, useRef } from "react";
import { useTranslation } from "react-i18next";
import { ToggleGroup, ToggleGroupItem } from "@/components/ui/toggle-group";
import type { Lang } from "@/lib/i18n";

export function LanguageOptions({
  value,
  pending,
  onChange,
}: {
  value: Lang;
  pending: boolean;
  onChange: (language: Lang) => void;
}) {
  const { t } = useTranslation();
  const group = useRef<HTMLDivElement>(null);
  const focusAfterSave = useRef<HTMLButtonElement | null>(null);
  useEffect(() => {
    if (pending || !focusAfterSave.current) return;
    if (document.activeElement === document.body)
      focusAfterSave.current.focus({ preventScroll: true });
    focusAfterSave.current = null;
  }, [pending]);
  return (
    <ToggleGroup
      ref={group}
      type="single"
      variant="outline"
      spacing={0}
      className="settings-language-options"
      value={value}
      disabled={pending}
      aria-label={t("language")}
      aria-busy={pending}
      onValueChange={(next) => {
        if (next !== "zh" && next !== "en") return;
        const focused = document.activeElement;
        if (
          focused instanceof HTMLButtonElement &&
          group.current?.contains(focused)
        )
          focusAfterSave.current = focused;
        onChange(next);
      }}
    >
      <ToggleGroupItem value="zh">中文</ToggleGroupItem>
      <ToggleGroupItem value="en">English</ToggleGroupItem>
    </ToggleGroup>
  );
}
