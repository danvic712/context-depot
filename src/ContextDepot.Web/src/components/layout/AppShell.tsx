import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { Header } from "./Header";
import { Sidebar } from "./Sidebar";
import type { AppContext } from "@/hooks/use-app-context";
import { cn } from "@/lib/utils";

export function AppShell({
  preferences,
  onSearch,
  home = false,
  page,
  pending = false,
  feedback,
  children,
}: {
  preferences: Omit<AppContext, "onSearch">;
  onSearch: () => void;
  home?: boolean;
  page: string;
  pending?: boolean;
  feedback?: ReactNode;
  children: ReactNode;
}) {
  const { t } = useTranslation();
  return (
    <div className="shell shell-ready">
      <Sidebar />
      <div className={cn("frame", home && "frame-home")}>
        <Header
          theme={preferences.theme}
          lang={preferences.language}
          languagePending={preferences.languagePending}
          appearancePending={preferences.appearancePending}
          onTheme={preferences.onTheme}
          onLanguage={preferences.onLanguage}
          onSearch={onSearch}
        />
        {feedback}
        <main className={cn("page", page)} aria-busy={pending}>
          {children}
        </main>
        <footer>
          <span>ContextDepot</span>
          <span>{t("footerTagline")}</span>
        </footer>
      </div>
    </div>
  );
}
