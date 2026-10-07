import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { Header } from "@/components/layout/Header";
import type { AppContext } from "@/hooks/use-app-context";
import "@/styles/setup.css";

export function SetupFrame({
  preferences,
  feedback,
  children,
}: {
  preferences: AppContext;
  feedback?: ReactNode;
  children: ReactNode;
}) {
  const { t } = useTranslation();
  return (
    <div className="setup-frame frame">
      <a className="skip-link" href="#main-content">
        {t("skipToContent")}
      </a>
      <Header
        showBrandIcon
        theme={preferences.theme}
        lang={preferences.language}
        languagePending={preferences.languagePending}
        appearancePending={preferences.appearancePending}
        onTheme={preferences.onTheme}
        onLanguage={preferences.onLanguage}
      />
      {feedback}
      <main id="main-content" tabIndex={-1} className="page setup-main">
        {children}
      </main>
      <footer>
        <span>ContextDepot</span>
        <span>{t("footerTagline")}</span>
      </footer>
    </div>
  );
}
