import { useEffect, useState } from "react";
import {
  MonitorIcon,
  DatabaseIcon,
  SearchIcon,
  LinkIcon,
  InfoIcon,
  FolderIcon,
  FileTextIcon,
  ExternalLinkIcon,
} from "lucide-react";
import { useTranslation } from "react-i18next";
import { useAppContext } from "@/hooks/use-app-context";
import type { Lang } from "@/lib/i18n";
import { ToggleGroup, ToggleGroupItem } from "@/components/ui/toggle-group";
import { AppSelect } from "@/components/ui/AppSelect";
import { Button } from "@/components/ui/button";
import { PageHeader } from "@/components/content/PageHeader";
import { Field, FieldDescription, FieldLabel } from "@/components/ui/field";
import { AccessKeySettings } from "@/features/settings/AccessKeySettings";
import { AiSettings } from "@/features/settings/AiSettings";
import { SettingsResourceState } from "@/features/settings/SettingsResourceState";
import {
  SettingsStatus,
  CopySetting,
} from "@/features/settings/SettingsStatus";
import { getSettingsOverview } from "@/features/settings/settings-api";
import { useSettingsResource } from "@/features/settings/use-settings-resource";
import { SettingsSection } from "./SettingsSection";
import "@/styles/settings.css";

const sections = [
  ["appearance", "general"],
  ["storage", "storage"],
  ["retrieval", "retrieval"],
  ["ai", "settingsAiNav"],
  ["access-keys", "settingsKeysNav"],
  ["connections", "connections"],
  ["about", "about"],
] as const;
export function Settings() {
  const {
    theme,
    language,
    languagePending,
    appearancePending,
    onTheme,
    onLanguage,
  } = useAppContext();
  const { t } = useTranslation();
  const resource = useSettingsResource(getSettingsOverview, true);
  const [active, setActive] = useState("appearance");
  const overview = resource.data;
  const endpoint = overview
    ? new URL(overview.mcpPath, window.location.href).href
    : undefined;
  useEffect(() => {
    const update = () => {
      const offset = 180;
      let next: string = "appearance";
      for (const [id] of sections) {
        if (
          (document.getElementById(id)?.getBoundingClientRect().top ??
            Infinity) <= offset
        )
          next = id;
      }
      setActive(next);
    };
    const hash = window.location.hash.slice(1);
    const frame = requestAnimationFrame(() => {
      if (sections.some(([id]) => id === hash))
        document.getElementById(hash)?.scrollIntoView();
      update();
    });
    window.addEventListener("scroll", update, { passive: true });
    window.addEventListener("resize", update);
    return () => {
      cancelAnimationFrame(frame);
      window.removeEventListener("scroll", update);
      window.removeEventListener("resize", update);
    };
  }, []);
  return (
    <>
      <PageHeader
        eyebrow={t("settingsKicker")}
        title={t("settingsTitle")}
        description={t("settingsSub")}
      />
      <nav className="section-nav" aria-label={t("settings")}>
        {sections.map(([id, label]) => (
          <a
            key={id}
            href={`#${id}`}
            aria-current={active === id ? "location" : undefined}
            onClick={() => setActive(id)}
          >
            {t(label)}
          </a>
        ))}
      </nav>
      <div className="settings-stack">
        <SettingsSection
          id="appearance"
          title={t("appearance")}
          detail={t("appearanceWhy")}
          icon={MonitorIcon}
        >
          <div className="settings-appearance-grid">
            <Field>
              <FieldLabel>{t("theme")}</FieldLabel>
              <ToggleGroup
                type="single"
                className="settings-theme-options"
                value={theme}
                disabled={appearancePending}
                aria-label={t("theme")}
                onValueChange={(value) => {
                  if (
                    value === "light" ||
                    value === "dark" ||
                    value === "system"
                  )
                    onTheme(value);
                }}
              >
                {(["light", "dark", "system"] as const).map((value) => (
                  <ToggleGroupItem key={value} value={value}>
                    {t(value)}
                  </ToggleGroupItem>
                ))}
              </ToggleGroup>
              <FieldDescription>{t("deploymentDefault")}</FieldDescription>
            </Field>
            <Field>
              <FieldLabel htmlFor="lang-select">{t("language")}</FieldLabel>
              <AppSelect
                id="lang-select"
                label={t("language")}
                value={language}
                onChange={(value) => onLanguage(value as Lang)}
                disabled={appearancePending}
                options={[
                  { value: "zh", label: "中文" },
                  { value: "en", label: "English" },
                ]}
              />
              <FieldDescription>
                {t(languagePending ? "loading" : "deploymentDefault")}
              </FieldDescription>
            </Field>
          </div>
        </SettingsSection>
        <SettingsSection
          id="storage"
          title={t("storage")}
          detail={t("storageWhy")}
          icon={DatabaseIcon}
        >
          <SettingsResourceState
            resource={resource}
            onRetry={resource.refresh}
          />
          <div className="settings-storage-grid">
            {(
              [
                {
                  title: "structured",
                  why: "structuredWhy",
                  icon: FolderIcon,
                  storage: "PostgreSQL",
                  state: overview?.databaseState,
                },
                {
                  title: "documents",
                  why: "documentsWhy",
                  icon: FileTextIcon,
                  storage: "Canonical Markdown",
                  state: overview?.markdownState,
                },
              ] as const
            ).map((item) => (
              <article className="settings-storage-tile" key={item.title}>
                <item.icon aria-hidden="true" />
                <div>
                  <h3>{t(item.title)}</h3>
                  <p>{t(item.why)}</p>
                  <dl className="settings-definition-list">
                    <div>
                      <dt>{t("settingsStorageType")}</dt>
                      <dd>{item.storage}</dd>
                    </div>
                    <div>
                      <dt>{t("settingsStatus")}</dt>
                      <dd>
                        <SettingsStatus state={item.state ?? "unknown"} />
                      </dd>
                    </div>
                    <div>
                      <dt>{t("settingsManagedBy")}</dt>
                      <dd>{t("settingsDeployment")}</dd>
                    </div>
                  </dl>
                </div>
              </article>
            ))}
          </div>
        </SettingsSection>
        <SettingsSection
          id="retrieval"
          title={t("retrieval")}
          detail={t("retrievalWhy")}
          icon={SearchIcon}
        >
          <div className="settings-retrieval-grid">
            <div>
              <div className="settings-detail-row">
                <div>
                  <h3>{t("keyword")}</h3>
                  <p>{t("settingsKeywordWhy")}</p>
                </div>
                <SettingsStatus state={overview?.databaseState ?? "unknown"} />
              </div>
              <div className="settings-detail-row">
                <div>
                  <h3>{t("semantic")}</h3>
                  <p>{t("settingsSemanticWhy")}</p>
                </div>
                <SettingsStatus state={overview?.semanticState ?? "unknown"} />
              </div>
            </div>
            <div>
              <div className="settings-detail-row">
                <div>
                  <h3>{t("embedding")}</h3>
                  <p>{t("settingsEmbeddingLinkWhy")}</p>
                </div>
                <a className="settings-text-link" href="#ai">
                  {t("settingsAiNav")} →
                </a>
              </div>
              <div className="settings-detail-row">
                <div>
                  <h3>{t("index")}</h3>
                  <p>
                    {overview?.totalCount !== null &&
                    overview?.totalCount !== undefined
                      ? t("settingsIndexCoverage", {
                          indexed: overview.indexedCount,
                          total: overview.totalCount,
                        })
                      : t("settingsIndexWhy")}
                  </p>
                </div>
                <SettingsStatus state={overview?.indexState ?? "unknown"} />
              </div>
            </div>
          </div>
        </SettingsSection>
        <AiSettings onChanged={resource.refresh} />
        <AccessKeySettings />
        <SettingsSection
          id="connections"
          title={t("connections")}
          detail={t("connectionsWhy")}
          icon={LinkIcon}
        >
          <div className="settings-connections-grid">
            <div>
              <h3>{t("endpoint")}</h3>
              {endpoint ? (
                <div className="settings-endpoint">
                  <code>{endpoint}</code>
                  <CopySetting
                    value={endpoint}
                    label={t("settingsCopyEndpoint")}
                  />
                </div>
              ) : (
                <p>{t("addressMissing")}</p>
              )}
              <p className="settings-inline-note">
                {t("settingsHeaderName")} <code>X-ContextDepot-Key</code>
              </p>
              {endpoint && (
                <details className="settings-connection-example">
                  <summary>{t("settingsConnectionExample")}</summary>
                  <p>{t("settingsConnectionReplace")}</p>
                  <pre>
                    {JSON.stringify(
                      {
                        mcpServers: {
                          "context-depot": {
                            url: endpoint,
                            headers: { "X-ContextDepot-Key": "<access-key>" },
                          },
                        },
                      },
                      null,
                      2,
                    )}
                  </pre>
                </details>
              )}
            </div>
            <div className="settings-connection-help">
              <h3>{t("help")}</h3>
              <ol>
                <li>{t("settingsConnectStep1")}</li>
                <li>{t("settingsConnectStep2")}</li>
                <li>{t("settingsConnectStep3")}</li>
              </ol>
              <a
                href="https://github.com/danvic712/context-depot#http-endpoints"
                target="_blank"
                rel="noreferrer"
              >
                {t("settingsIntegrationGuide")}{" "}
                <ExternalLinkIcon size={13} aria-hidden="true" />
              </a>
            </div>
          </div>
        </SettingsSection>
        <SettingsSection
          id="about"
          title="ContextDepot"
          detail={t("aboutWhy")}
          icon={InfoIcon}
        >
          <div className="settings-about-row">
            {overview && (
              <span>
                {t("settingsDepot")}: <strong>{overview.depotName}</strong>
              </span>
            )}
            {overview?.version && (
              <span>
                {t("version")}: <code>{overview.version.split("+")[0]}</code>
              </span>
            )}
            <Button variant="outline" size="sm" asChild>
              <a
                href="https://github.com/danvic712/context-depot"
                target="_blank"
                rel="noreferrer"
              >
                <ExternalLinkIcon aria-hidden="true" />
                {t("settingsRepository")}
              </a>
            </Button>
          </div>
        </SettingsSection>
      </div>
    </>
  );
}
