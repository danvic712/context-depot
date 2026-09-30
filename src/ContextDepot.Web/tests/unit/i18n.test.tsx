import { describe, expect, test } from "bun:test";
import { Glob } from "bun";
import { renderToStaticMarkup } from "react-dom/server";
import { useTranslation } from "react-i18next";
import i18n, {
  initializeI18n,
  changeLanguage,
  type Lang,
  type Messages,
  type TranslationLoader,
} from "../../src/lib/i18n";

async function readMessages(language: Lang): Promise<Messages> {
  const locale = language === "zh" ? "zh-CN" : "en-US";
  const directory = new URL(`../../../../locales/${locale}/`, import.meta.url);
  const glob = new Glob("{navigation-and-actions,home-overview,knowledge-search,workspace-browser,knowledge-actions,application-settings,ui-states}.json");
  const messages = [];
  for await (const file of glob.scan({ cwd: directory.pathname })) {
    messages.push(await Bun.file(new URL(file, directory)).json());
  }
  expect(messages).toHaveLength(7);
  return Object.assign({}, ...messages);
}

function NavigationLabel() {
  const { t } = useTranslation();
  return <span>{t("search")}</span>;
}

function recordedLoader() {
  const languages: Lang[] = [];
  const loader: TranslationLoader = async (language) => {
    languages.push(language);
    return readMessages(language);
  };
  return { languages, loader };
}

describe("UI translations", () => {
  test("English and Chinese provide the same translation keys", async () => {
    const [en, zh] = await Promise.all([readMessages("en"), readMessages("zh")]);
    expect(Object.keys(zh).sort()).toEqual(Object.keys(en).sort());
  });

  test("a Chinese startup loads only Chinese before rendering", async () => {
    const { languages, loader } = recordedLoader();
    await initializeI18n("zh", loader);
    expect(languages).toEqual(["zh"]);
    expect(i18n.hasResourceBundle("en", "translation")).toBe(false);
    expect(renderToStaticMarkup(<NavigationLabel />)).toBe("<span>搜索</span>");
  });

  test("switching loads the new language once and reuses loaded resources", async () => {
    const { languages, loader } = recordedLoader();
    await initializeI18n("en", loader);
    expect(languages).toEqual(["en"]);
    expect(i18n.hasResourceBundle("zh", "translation")).toBe(false);
    await changeLanguage("zh", loader);
    expect(renderToStaticMarkup(<NavigationLabel />)).toBe("<span>搜索</span>");
    await changeLanguage("en", loader);
    expect(renderToStaticMarkup(<NavigationLabel />)).toBe("<span>Search</span>");
    await changeLanguage("zh", loader);
    expect(languages).toEqual(["en", "zh"]);
  });

  test("a failed Chinese load requests English as a fallback", async () => {
    const languages: Lang[] = [];
    await initializeI18n("zh", async (language) => {
      languages.push(language);
      if (language === "zh") throw new Error("Network unavailable");
      return readMessages(language);
    });
    expect(languages).toEqual(["zh", "en"]);
    expect(i18n.resolvedLanguage).toBe("en");
    expect(renderToStaticMarkup(<NavigationLabel />)).toBe("<span>Search</span>");
  });

  test("a slower previous language selection cannot overwrite the latest one", async () => {
    await initializeI18n("en", readMessages);
    const zh = await readMessages("zh");
    let finish!: (messages: Messages) => void;
    const older = changeLanguage("zh", () => new Promise((resolve) => { finish = resolve; }));
    expect(await changeLanguage("en", readMessages)).toBe("en");
    finish(zh);
    expect(await older).toBeNull();
    expect(i18n.resolvedLanguage).toBe("en");
  });

  test("a failed request can be retried without losing the active language", async () => {
    await initializeI18n("zh", readMessages);
    await expect(changeLanguage("en", async () => { throw new Error("Offline"); })).rejects.toThrow("Offline");
    expect(i18n.resolvedLanguage).toBe("zh");
    expect(await changeLanguage("en", readMessages)).toBe("en");
  });
});
