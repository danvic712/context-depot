import { useOutletContext, type NavigateOptions, type To } from "react-router";
import type { PreviewState } from "@/components/feedback/StatePreview";
import type { SampleKnowledge } from "@/features/knowledge/sample-data";
import type { Theme } from "@/features/settings/browser-preferences";
import type { Lang } from "@/lib/i18n";

export type PageId =
  "home" | "search" | "spaces" | "space" | "context" | "document" | "settings";
export type NavigationItem = "home" | "search" | "spaces" | "settings";

export interface PageHandle {
  navigation: NavigationItem;
  page: PageId;
  title: "home" | "search" | "spaces" | "settings";
  previewControls?: boolean;
}

export interface AppContext {
  preview: boolean;
  state: PreviewState;
  onRetry: () => void;
  query: string;
  params: URLSearchParams;
  results: readonly SampleKnowledge[];
  onSearch: (query: string) => void;
  onFilter: (key: "type" | "kind" | "workspace", value: string) => void;
  navigate: (path: string, options?: NavigateOptions) => void;
  linkTo: (path: string) => To;
  selectedId: string;
  view: string;
  item?: SampleKnowledge;
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
