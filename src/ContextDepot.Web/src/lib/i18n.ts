import i18n from "i18next";
import { initReactI18next } from "react-i18next";
import { load } from "../features/settings/browser-preferences";

// Type-only references preserve translation-key checking without bundling JSON.
export type Messages =
  typeof import("../../../../locales/en-US/navigation-and-actions.json") &
    typeof import("../../../../locales/en-US/home-overview.json") &
    typeof import("../../../../locales/en-US/knowledge-search.json") &
    typeof import("../../../../locales/en-US/workspace-browser.json") &
    typeof import("../../../../locales/en-US/knowledge-actions.json") &
    typeof import("../../../../locales/en-US/application-settings.json") &
    typeof import("../../../../locales/en-US/ui-states.json");
export type Lang = "en" | "zh";
export type TranslationLoader = (language: Lang) => Promise<Messages>;

declare module "i18next" {
  interface CustomTypeOptions {
    defaultNS: "translation";
    resources: { translation: Messages };
  }
}

const pending = new Map<Lang, Promise<void>>();
const defaultLoader: TranslationLoader = async (language) => {
  const { loadTranslations } = await import("./translation-loader");
  return loadTranslations(language);
};

async function ensureLanguage(language: Lang, loader: TranslationLoader) {
  if (i18n.hasResourceBundle(language, "translation")) return;
  let request = pending.get(language);
  if (!request) {
    request = loader(language)
      .then((messages) => {
        i18n.addResourceBundle(language, "translation", messages);
      })
      .finally(() => pending.delete(language));
    pending.set(language, request);
  }
  await request;
}

async function resolveLanguage(language: Lang, loader: TranslationLoader) {
  try {
    await ensureLanguage(language, loader);
    return language;
  } catch (error) {
    if (language === "en") throw error;
    await ensureLanguage("en", loader);
    return "en" as const;
  }
}

export async function prepareLanguage(language: Lang) {
  await ensureLanguage(language, defaultLoader);
}

export async function initializeI18n(
  language: Lang = load("contextdepot.language", ["en", "zh"]) ?? "en",
  loader: TranslationLoader = defaultLoader,
) {
  await i18n.use(initReactI18next).init({
    resources: {},
    lng: language,
    fallbackLng: "en",
    supportedLngs: ["en", "zh"],
    interpolation: { escapeValue: false },
  });
  const resolved = await resolveLanguage(language, loader);
  await i18n.changeLanguage(resolved);
}

let languageRequest = 0;

export async function changeLanguage(
  language: Lang,
  loader: TranslationLoader = defaultLoader,
): Promise<Lang | null> {
  const request = ++languageRequest;
  const resolved = await resolveLanguage(language, loader);
  // A slower earlier request must not overwrite a later language selection.
  if (request !== languageRequest) return null;
  await i18n.changeLanguage(resolved);
  return resolved;
}

export default i18n;
