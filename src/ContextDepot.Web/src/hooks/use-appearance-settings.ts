import { useCallback, useEffect, useRef, useState } from "react";
import { useTheme } from "next-themes";
import { useTranslation } from "react-i18next";
import { changeLanguage, prepareLanguage, type Lang } from "@/lib/i18n";
import { isRequestCanceled } from "@/lib/http-client";
import { save, type Theme } from "@/features/settings/browser-preferences";
import {
  getAppearance,
  getInitialAppearance,
  setAppearanceTheme,
  setAppearanceLanguage,
} from "@/features/settings/appearance-api";
import {
  resolveAppearancePreferences,
  setupAppearanceKeys,
  type AppearancePreferences,
} from "@/features/settings/appearance-preferences";

export function useAppearanceSettings(setup = false) {
  const { i18n } = useTranslation();
  const { setTheme: applyTheme } = useTheme();
  const [theme, setTheme] = useState<Theme>(
    () =>
      resolveAppearancePreferences(getInitialAppearance().settings, setup)
        .theme,
  );
  const [language, setLanguage] = useState<Lang>(() =>
    i18n.resolvedLanguage === "zh" ? "zh" : "en",
  );
  const [pending, setPending] = useState<"theme" | "language" | null>(null);
  const [appearanceError, setAppearanceError] = useState(
    !getInitialAppearance().available,
  );
  const saving = useRef(false);
  const setupSelection = useRef<Partial<AppearancePreferences>>({});
  const refreshing = useRef(false);
  const [appearanceRefreshPending, setAppearanceRefreshPending] =
    useState(false);
  const revision = useRef(0);

  const refreshAppearance = useCallback(
    async (signal?: AbortSignal) => {
      if (saving.current || refreshing.current) return;
      refreshing.current = true;
      setAppearanceRefreshPending(true);
      const request = ++revision.current;
      try {
        const settings = await getAppearance(signal);
        if (signal?.aborted || saving.current || request !== revision.current)
          return;
        const { theme: effectiveTheme, language: effectiveLanguage } =
          resolveAppearancePreferences(settings, setup, setupSelection.current);
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
      } finally {
        refreshing.current = false;
        setAppearanceRefreshPending(false);
      }
    },
    [applyTheme, i18n, setup],
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
          if (setup) {
            setupSelection.current.theme = saved;
            save(setupAppearanceKeys.theme, saved);
          }
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
    [applyTheme, setup],
  );

  const onLanguage = useCallback(
    (value: Lang) => {
      if (saving.current) return;
      saving.current = true;
      ++revision.current;
      setPending("language");
      void (async () => {
        try {
          const saved = await setAppearanceLanguage(value);
          const resolved = await changeLanguage(saved);
          if (resolved) {
            if (setup) {
              setupSelection.current.language = resolved;
              save(setupAppearanceKeys.language, resolved);
            }
            save("contextdepot.language", null);
            setLanguage(resolved);
          }
          setAppearanceError(false);
        } finally {
          saving.current = false;
          setPending(null);
        }
      })().catch(() => undefined);
    },
    [setup],
  );

  return {
    theme,
    language,
    languagePending: pending === "language",
    appearancePending: pending !== null,
    appearanceError,
    appearanceRefreshPending,
    onTheme,
    onLanguage,
    refreshAppearance,
  };
}
