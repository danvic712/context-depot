import { useCallback, useEffect, useRef, useState } from "react";
import { useTheme } from "next-themes";
import { useTranslation } from "react-i18next";
import { changeLanguage, prepareLanguage, type Lang } from "@/lib/i18n";
import { isRequestCanceled } from "@/lib/http-client";
import {
  load,
  save,
  type Theme,
} from "@/features/settings/browser-preferences";
import {
  getAppearance,
  getInitialAppearance,
  setAppearanceTheme,
  setAppearanceLanguage,
  toUiLanguage,
} from "@/features/settings/appearance-api";

export function useAppearanceSettings() {
  const { i18n } = useTranslation();
  const { setTheme: applyTheme } = useTheme();
  const [theme, setTheme] = useState<Theme>(
    () =>
      load("contextdepot.theme", ["system", "light", "dark"]) ??
      getInitialAppearance().settings.theme,
  );
  const [language, setLanguage] = useState<Lang>(() =>
    i18n.resolvedLanguage === "zh" ? "zh" : "en",
  );
  const [pending, setPending] = useState<"theme" | "language" | null>(null);
  const [appearanceError, setAppearanceError] = useState(
    !getInitialAppearance().available,
  );
  const saving = useRef(false);
  const revision = useRef(0);

  const refreshAppearance = useCallback(
    async (signal?: AbortSignal) => {
      if (saving.current) return;
      const request = ++revision.current;
      try {
        const settings = await getAppearance(signal);
        if (signal?.aborted || saving.current || request !== revision.current)
          return;
        const effectiveTheme =
          load<Theme>("contextdepot.theme", ["system", "light", "dark"]) ??
          settings.theme;
        const effectiveLanguage =
          load<Lang>("contextdepot.language", ["en", "zh"]) ??
          toUiLanguage(settings.language);
        if (i18n.resolvedLanguage !== effectiveLanguage) {
          await prepareLanguage(effectiveLanguage);
          if (signal?.aborted || saving.current || request !== revision.current)
            return;
          const resolved = await changeLanguage(effectiveLanguage);
          if (resolved) setLanguage(resolved);
        }
        if (signal?.aborted || saving.current || request !== revision.current)
          return;
        setTheme(effectiveTheme);
        applyTheme(effectiveTheme);
        setAppearanceError(false);
      } catch (error) {
        if (
          !isRequestCanceled(error) &&
          !signal?.aborted &&
          request === revision.current
        )
          setAppearanceError(true);
      }
    },
    [applyTheme, i18n],
  );

  useEffect(() => {
    const controller = new AbortController();
    const timer = setInterval(() => {
      if (!document.hidden) void refreshAppearance(controller.signal);
    }, 30_000);
    const refresh = () => {
      void refreshAppearance(controller.signal);
    };
    const visible = () => {
      if (!document.hidden) refresh();
    };
    addEventListener("focus", refresh);
    addEventListener("storage", refresh);
    document.addEventListener("visibilitychange", visible);
    return () => {
      controller.abort();
      clearInterval(timer);
      removeEventListener("focus", refresh);
      removeEventListener("storage", refresh);
      document.removeEventListener("visibilitychange", visible);
    };
  }, [refreshAppearance]);

  const onTheme = useCallback(
    (value: Theme) => {
      if (saving.current) return;
      saving.current = true;
      ++revision.current;
      setPending("theme");
      void setAppearanceTheme(value)
        .then((saved) => {
          save("contextdepot.theme", null);
          setTheme(saved);
          applyTheme(saved);
          setAppearanceError(false);
        })
        .catch(() => undefined)
        .finally(() => {
          saving.current = false;
          setPending(null);
        });
    },
    [applyTheme],
  );

  const onLanguage = useCallback((value: Lang) => {
    if (saving.current) return;
    saving.current = true;
    ++revision.current;
    setPending("language");
    void (async () => {
      try {
        const saved = await setAppearanceLanguage(value);
        const resolved = await changeLanguage(saved);
        if (resolved) {
          save("contextdepot.language", null);
          setLanguage(resolved);
        }
        setAppearanceError(false);
      } finally {
        saving.current = false;
        setPending(null);
      }
    })().catch(() => undefined);
  }, []);

  return {
    theme,
    language,
    languagePending: pending === "language",
    appearancePending: pending !== null,
    appearanceError,
    onTheme,
    onLanguage,
    refreshAppearance,
  };
}
