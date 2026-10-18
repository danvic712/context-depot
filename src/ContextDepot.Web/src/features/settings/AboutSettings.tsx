import { ExternalLinkIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { Brand } from "@/components/Brand";
import { ApplicationVersion } from "@/components/meta/ApplicationVersion";
import { Skeleton } from "@/components/ui/skeleton";
import { SettingsSection } from "@/pages/settings/SettingsSection";
import { CopySetting } from "./SettingsStatus";

export function AboutSettings({
  version,
  versionPending,
  versionFailed,
  depotName,
  depotPending,
}: {
  version: string | null | undefined;
  versionPending: boolean;
  versionFailed: boolean;
  depotName: string | undefined;
  depotPending: boolean;
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
        <div>
          <dt>{t("settingsDepot")}</dt>
          <dd aria-live="polite" aria-busy={depotPending}>
            {depotName ? (
              <span>{depotName}</span>
            ) : depotPending ? (
              <>
                <Skeleton className="h-4 w-28" aria-hidden="true" />
                <span className="sr-only">{t("loading")}</span>
              </>
            ) : (
              <span className="settings-about-unavailable">
                {t("settingsLibraryUnavailable")}
              </span>
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
