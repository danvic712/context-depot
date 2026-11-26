import { ExternalLinkIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { Brand } from "@/components/Brand";
import { ApplicationVersion } from "@/components/meta/ApplicationVersion";
import { SettingsSection } from "@/pages/settings/SettingsSection";
import { CopySetting } from "./SettingsStatus";

export function AboutSettings({
  version,
  versionPending,
  versionFailed,
}: {
  version: string | null | undefined;
  versionPending: boolean;
  versionFailed: boolean;
}) {
  const { t } = useTranslation();
  return (
    <SettingsSection
      id="about"
      title="ContextDepot"
      detail={t("aboutWhy")}
      visual={<Brand />}
    >
      <dl className="settings-about-details">
        <div>
          <dt>{t("settingsCurrentVersion")}</dt>
          <dd>
            <ApplicationVersion
              version={version}
              pending={versionPending}
              failed={versionFailed}
              showLabel={false}
            />
            {version && !versionPending && !versionFailed && (
              <CopySetting value={version} label={t("settingsCopyVersion")} />
            )}
          </dd>
        </div>
      </dl>
      <a
        className="settings-text-link settings-about-link"
        href="https://github.com/danvic712/context-depot"
        target="_blank"
        rel="noreferrer"
      >
        {t("settingsRepository")}
        <ExternalLinkIcon aria-hidden="true" />
      </a>
    </SettingsSection>
  );
}
