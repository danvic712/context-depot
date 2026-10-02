import { useOutletContext } from "react-router";
import type { Theme } from "@/features/settings/browser-preferences";
import type { Lang } from "@/lib/i18n";

export type PageId =
  "home" | "search" | "spaces" | "space" | "context" | "document" | "settings";
export type NavigationItem = "home" | "search" | "spaces" | "settings";

export interface PageHandle {
  navigation: NavigationItem;
  page: PageId;
  title: "home" | "search" | "spaces" | "settings";
}

export interface AppContext {
  onSearch: (query: string) => void;
  onBack: () => void;
  theme: Theme;
  language: Lang;
  languagePending: boolean;
  appearancePending: boolean;
  onTheme: (value: Theme) => void;
  onLanguage: (value: Lang) => void;
}

export function useAppContext() {
  return useOutletContext<AppContext>();
}
