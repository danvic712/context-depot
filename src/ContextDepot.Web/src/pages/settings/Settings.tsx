import { useAppContext } from "@/hooks/use-app-context";
import type { Theme } from "@/features/settings/browser-preferences";
import type { Lang } from "@/lib/i18n";
import { useTranslation } from "react-i18next";
import "@/styles/settings.css";
import { SettingsSection } from "./SettingsSection";
import { Heading } from "../../components/content/PageElements";
import { FolderIcon, FileTextIcon } from "lucide-react";
import { AppSelect } from "../../components/ui/AppSelect";
import {
  Field,
  FieldDescription,
  FieldGroup,
  FieldLabel,
} from "../../components/ui/field";

export function Settings() {
  const { theme, language, onTheme, onLanguage } = useAppContext();
  const { t } = useTranslation();
  return (
    <>
      <Heading
        kicker={t("settingsKicker")}
        title={t("settingsTitle")}
        sub={t("settingsSub")}
      />
      <nav className="section-nav" aria-label={t("settings")}>
        {[
          ["appearance", t("general")],
          ["storage", t("storage")],
          ["retrieval", t("retrieval")],
          ["connections", t("connections")],
          ["about", t("about")],
        ].map(([id, label]) => (
          <a key={id} href={`#${id}`}>
            {label}
          </a>
        ))}
      </nav>
      <div className="settings-stack">
        <SettingsSection
          id="appearance"
          title={t("appearance")}
          detail={t("appearanceWhy")}
        >
          <FieldGroup className="two-columns">
            <Field>
              <FieldLabel htmlFor="theme-select">{t("theme")}</FieldLabel>
              <AppSelect
                id="theme-select"
                label={t("theme")}
                value={theme ?? "default"}
                onChange={(value) =>
                  onTheme(value === "default" ? null : (value as Theme))
                }
                options={[
                  { value: "default", label: t("useDefault") },
                  { value: "system", label: t("system") },
                  { value: "light", label: t("light") },
                  { value: "dark", label: t("dark") },
                ]}
              />
              <FieldDescription>
                {theme ? t("browser") : t("following")} · {t("deployment")}:{" "}
                {t("unknown")}
              </FieldDescription>
            </Field>
            <Field>
              <FieldLabel htmlFor="lang-select">{t("language")}</FieldLabel>
              <AppSelect
                id="lang-select"
                label={t("language")}
                value={language ?? "default"}
                onChange={(value) =>
                  onLanguage(value === "default" ? null : (value as Lang))
                }
                options={[
                  { value: "default", label: t("useDefault") },
                  { value: "zh", label: "中文" },
                  { value: "en", label: "English" },
                ]}
              />
              <FieldDescription>
                {language ? t("browser") : t("following")} · {t("deployment")}:{" "}
                {t("unknown")}
              </FieldDescription>
            </Field>
          </FieldGroup>
        </SettingsSection>
        <SettingsSection
          id="storage"
          title={t("storage")}
          detail={t("storageWhy")}
        >
          <div className="two-columns">
            <div className="info-tile">
              <FolderIcon size={22} aria-hidden="true" />
              <div>
                <h3>{t("structured")}</h3>
                <p>{t("structuredWhy")}</p>
              </div>
            </div>
            <div className="info-tile">
              <FileTextIcon size={22} aria-hidden="true" />
              <div>
                <h3>{t("documents")}</h3>
                <p>{t("documentsWhy")}</p>
              </div>
            </div>
          </div>
        </SettingsSection>
        <SettingsSection
          id="retrieval"
          title={t("retrieval")}
          detail={t("retrievalWhy")}
        >
          <div className="status-grid">
            {[t("keyword"), t("semantic"), t("embedding"), t("index")].map(
              (label) => (
                <div className="status-row" key={label}>
                  <span>{label}</span>
                  <strong>{t("unknown")}</strong>
                </div>
              ),
            )}
          </div>
        </SettingsSection>
        <SettingsSection
          id="connections"
          title={t("connections")}
          detail={t("connectionsWhy")}
        >
          <div className="two-columns">
            <div>
              <h3>{t("endpoint")}</h3>
              <p>{t("addressMissing")}</p>
            </div>
            <div>
              <h3>{t("help")}</h3>
              <p>{t("helpWhy")}</p>
            </div>
          </div>
        </SettingsSection>
        <SettingsSection id="about" title="ContextDepot" detail={t("aboutWhy")}>
          <div className="status-row">
            <span>{t("version")}</span>
            <strong>{t("versionMissing")}</strong>
          </div>
        </SettingsSection>
      </div>
    </>
  );
}
