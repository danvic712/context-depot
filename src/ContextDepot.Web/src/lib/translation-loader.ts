import type { Lang, Messages } from "./i18n";

const modules = import.meta.glob<Partial<Messages>>(
  "../../../../locales/{en-US,zh-CN}/{navigation-and-actions,home-overview,knowledge-search,workspace-browser,application-settings,ui-states}.json",
  { import: "default" },
);

export async function loadTranslations(language: Lang): Promise<Messages> {
  const locale = language === "zh" ? "zh-CN" : "en-US";
  const loaders = Object.entries(modules)
    .filter(([path]) => path.includes(`/${locale}/`))
    .map(([, load]) => load);
  if (loaders.length !== 6) {
    throw new Error(`Incomplete UI translations for ${locale}`);
  }
  const messages = await Promise.all(loaders.map((load) => load()));
  return Object.assign({}, ...messages) as Messages;
}
