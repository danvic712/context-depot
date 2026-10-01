import { httpRequest } from "@/lib/http-client";
import type { Theme } from "./browser-preferences";
import i18n, { prepareLanguage, type Lang } from "@/lib/i18n";

const appearanceFeedbackId = "appearance-save";

export interface AppearanceSettings {
  theme: Theme;
  language: string;
}

let initial: AppearanceSettings = { theme: "system", language: "en-US" };
let available = false;

function themeValue(value: unknown): Theme {
  if (value === "system" || value === "light" || value === "dark") return value;
  throw new Error("Invalid appearance response");
}

function languageValue(value: unknown): string {
  if (
    typeof value === "string" &&
    /^[a-z]{2,3}(?:-[A-Za-z0-9]{2,8})*$/.test(value)
  )
    return value;
  throw new Error("Invalid appearance response");
}

function objectValue(value: unknown): Record<string, unknown> {
  if (value && typeof value === "object" && !Array.isArray(value))
    return value as Record<string, unknown>;
  throw new Error("Invalid appearance response");
}

export function getInitialAppearance() {
  return { settings: initial, available };
}

export function toUiLanguage(language: string): Lang {
  return language.toLowerCase().startsWith("zh") ? "zh" : "en";
}

export async function getAppearance(
  signal?: AbortSignal,
): Promise<AppearanceSettings> {
  initial = await httpRequest({
    method: "GET",
    url: "/settings/appearance",
    signal,
    parse: (data) => {
      const value = objectValue(data);
      return {
        theme: themeValue(value.theme),
        language: languageValue(value.language),
      };
    },
  });
  available = true;
  return initial;
}

export async function setAppearanceTheme(theme: Theme): Promise<Theme> {
  const saved = await httpRequest({
    method: "PUT",
    url: "/settings/appearance/theme",
    data: { theme },
    parse: (data) => themeValue(objectValue(data).theme),
    feedback: {
      id: appearanceFeedbackId,
      success: () => i18n.t("themeChanged", { theme: i18n.t(theme) }),
      error: () => i18n.t("themeChangeError"),
    },
  });
  initial = { ...initial, theme: saved };
  return saved;
}

export async function setAppearanceLanguage(language: Lang): Promise<Lang> {
  const saved = await httpRequest({
    method: "PUT",
    url: "/settings/appearance/language",
    data: {
      language: language === "zh" ? "zh-CN" : "en-US",
    },
    beforeRequest: () => prepareLanguage(language),
    parse: (data) => languageValue(objectValue(data).language),
    feedback: {
      id: appearanceFeedbackId,
      success: () =>
        i18n.t("languageChanged", {
          lng: language,
          language: language === "zh" ? "中文" : "English",
        }),
      error: () => i18n.t("languageChangeError"),
    },
  });
  initial = { ...initial, language: saved };
  return toUiLanguage(saved);
}
