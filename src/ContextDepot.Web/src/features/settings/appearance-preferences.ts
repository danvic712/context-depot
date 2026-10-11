import type { Lang } from "@/lib/i18n";
import { toUiLanguage, type AppearanceSettings } from "./appearance-api";
import { browserLanguage, load, save, type Theme } from "./browser-preferences";

export interface AppearancePreferences {
  theme: Theme;
  language: Lang;
}

export const setupAppearanceKeys = {
  theme: "contextdepot.setup.theme",
  language: "contextdepot.setup.language",
} as const;

export function resolveAppearancePreferences(
  settings: AppearanceSettings,
  setup: boolean,
  selection: Partial<AppearancePreferences> = {},
): AppearancePreferences {
  if (setup)
    return {
      theme:
        load<Theme>(setupAppearanceKeys.theme, ["system", "light", "dark"]) ??
        selection.theme ??
        "system",
      language:
        load<Lang>(setupAppearanceKeys.language, ["en", "zh"]) ??
        selection.language ??
        browserLanguage(),
    };
  return {
    theme:
      load<Theme>("contextdepot.theme", ["system", "light", "dark"]) ??
      settings.theme,
    language:
      load<Lang>("contextdepot.language", ["en", "zh"]) ??
      toUiLanguage(settings.language),
  };
}

export function rememberSetupAppearance(preferences: AppearancePreferences) {
  save("contextdepot.theme", preferences.theme);
  save("contextdepot.language", preferences.language);
  save(setupAppearanceKeys.theme, null);
  save(setupAppearanceKeys.language, null);
}
