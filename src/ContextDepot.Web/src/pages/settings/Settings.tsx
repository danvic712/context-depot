import { useEffect, useRef, useState } from "react";
import {
  MonitorIcon,
  DatabaseIcon,
  SearchIcon,
  LinkIcon,
  InfoIcon,
  FolderIcon,
  FileTextIcon,
  ExternalLinkIcon,
  SparklesIcon,
  KeyRoundIcon,
  ChevronRightIcon,
  CheckIcon,
  NetworkIcon,
  ChevronDownIcon,
} from "lucide-react";
import { useTranslation } from "react-i18next";
import { useAppContext } from "@/hooks/use-app-context";
import { ThemeOptions } from "@/features/settings/ThemeOptions";
import { LanguageOptions } from "@/features/settings/LanguageOptions";
import { useSettingsNavigationMotion } from "@/features/settings/use-settings-navigation-motion";
import { Spinner } from "@/components/ui/spinner";
import { Button } from "@/components/ui/button";
import {
  Collapsible,
  CollapsibleTrigger,
  CollapsibleContent,
} from "@/components/ui/collapsible";
import { PageHeader } from "@/components/content/PageHeader";
import { Field, FieldGroup, FieldLabel } from "@/components/ui/field";
import { AccessKeySettings } from "@/features/settings/AccessKeySettings";
import { AboutSettings } from "@/features/settings/AboutSettings";
import { InferenceSettings } from "@/features/settings/InferenceSettings";
import { SettingsResourceState } from "@/features/settings/SettingsResourceState";
import {
  SettingsStatus,
  CopySetting,
} from "@/features/settings/SettingsStatus";
import { getSettingsOverview } from "@/features/settings/settings-api";
import { getApplicationMeta } from "@/lib/meta-api";
import { useRequestResource } from "@/hooks/use-request-resource";
import { useSettingsResource } from "@/features/settings/use-settings-resource";
import { SettingsSection } from "./SettingsSection";
import "@/styles/settings.css";

const sections = [
  ["appearance", "appearance", MonitorIcon],
  ["storage", "storage", DatabaseIcon],
  ["retrieval", "retrieval", SearchIcon],
  ["inference", "settingsInferenceNav", SparklesIcon],
  ["access-keys", "settingsKeysNav", KeyRoundIcon],
  ["connections", "connections", LinkIcon],
  ["about", "about", InfoIcon],
] as const;
export function Settings() {
  const { theme, language, appearancePending, onTheme, onLanguage } =
    useAppContext();
  const { t } = useTranslation();
  const resource = useSettingsResource(getSettingsOverview, true);
  const meta = useRequestResource("application-meta", 0, getApplicationMeta);
  const [active, setActive] = useState("appearance");
  const navRef = useRef<HTMLElement>(null);
  useSettingsNavigationMotion(navRef, active);
  const overview = resource.data;
  const endpoint = overview
    ? new URL(overview.mcpPath, window.location.href).href
    : undefined;
  useEffect(() => {
    const update = () => {
      if (
        window.scrollY > 0 &&
        window.scrollY + window.innerHeight >=
          document.documentElement.scrollHeight - 2
      ) {
        setActive("about");
        return;
      }
      const nav = document.querySelector(".section-nav");
      const offset = window.matchMedia("(max-width: 1000px)").matches
        ? (nav?.getBoundingClientRect().bottom ?? 144) + 24
        : 120;
      let next: string = "appearance";
      for (const [id] of sections) {
        if (
          (document.getElementById(id)?.getBoundingClientRect().top ??
            Infinity) <=
          offset + 1
        )
          next = id;
      }
      setActive(next);
    };
    const hash = window.location.hash.slice(1);
    const stack = document.querySelector(".settings-stack");
    let restoreHash = sections.some(([id]) => id === hash);
    const layoutChanged = () => {
      if (restoreHash) {
        document.getElementById(hash)?.scrollIntoView();
        if (!stack?.querySelector(".settings-loading")) restoreHash = false;
      }
      update();
    };
    const cancelRestore = () => {
      restoreHash = false;
    };
    const frame = requestAnimationFrame(layoutChanged);
    const observer = new ResizeObserver(layoutChanged);
    if (stack) observer.observe(stack);
    window.addEventListener("scroll", update, { passive: true });
    window.addEventListener("resize", update);
    window.addEventListener("wheel", cancelRestore, { passive: true });
    window.addEventListener("touchstart", cancelRestore, { passive: true });
    window.addEventListener("keydown", cancelRestore);
    return () => {
      cancelAnimationFrame(frame);
      observer.disconnect();
      window.removeEventListener("scroll", update);
      window.removeEventListener("resize", update);
      window.removeEventListener("wheel", cancelRestore);
      window.removeEventListener("touchstart", cancelRestore);
      window.removeEventListener("keydown", cancelRestore);
    };
  }, []);
  useEffect(() => {
    const nav = document.querySelector<HTMLElement>(".section-nav");
    const revealActive = () => {
      const link = nav?.querySelector<HTMLElement>("[aria-current]");
      if (nav && link && nav.scrollWidth > nav.clientWidth)
        nav.scrollTo({
          left: link.offsetLeft - (nav.clientWidth - link.offsetWidth) / 2,
          behavior: "instant",
        });
    };
    revealActive();
    window.addEventListener("resize", revealActive);
    return () => window.removeEventListener("resize", revealActive);
  }, [active]);
  return (
    <div className="settings-layout">
      <div className="settings-sidebar">
        <PageHeader title={t("settingsTitle")} description={t("settingsSub")} />
        <nav ref={navRef} className="section-nav" aria-label={t("settings")}>
          <span className="settings-nav-indicator" aria-hidden="true" />
          {sections.map(([id, label, Icon]) => (
            <a
              key={id}
              href={`#${id}`}
              data-navigation-item={id}
              aria-current={active === id ? "location" : undefined}
              onClick={() => setActive(id)}
            >
              <Icon aria-hidden="true" />
              <span>{t(label)}</span>
              <ChevronRightIcon
                className="settings-nav-chevron"
                aria-hidden="true"
              />
            </a>
          ))}
        </nav>
      </div>
      <div className="settings-stack">
        <SettingsSection
          id="appearance"
          title={t("appearance")}
          detail={t("appearanceWhy")}
          icon={MonitorIcon}
          action={
            <span className="settings-save-status" role="status">
              {appearancePending ? (
                <Spinner aria-hidden="true" />
              ) : (
                <CheckIcon aria-hidden="true" />
              )}
              {t(appearancePending ? "settingsSaving" : "settingsAutoSave")}
            </span>
          }
        >
          <FieldGroup className="settings-appearance-grid">
            <Field>
              <FieldLabel>{t("theme")}</FieldLabel>
              <ThemeOptions
                value={theme}
                pending={appearancePending}
                onChange={onTheme}
              />
            </Field>
            <Field className="settings-language-field">
              <FieldLabel>{t("language")}</FieldLabel>
              <LanguageOptions
                value={language}
                onChange={onLanguage}
                pending={appearancePending}
              />
            </Field>
          </FieldGroup>
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
                  storage: t("settingsMarkdownFiles"),
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
                      <dd>
                        <span>{item.storage}</span>
                        <SettingsStatus state={item.state ?? "unknown"} />
                      </dd>
                    </div>
                  </dl>
                </div>
              </article>
            ))}
          </div>
          <p className="settings-storage-note">
            <InfoIcon aria-hidden="true" />
            {t("settingsStorageManaged")}
          </p>
        </SettingsSection>
        <SettingsSection
          id="retrieval"
          title={t("retrieval")}
          detail={t("retrievalWhy")}
          icon={SearchIcon}
        >
          <div className="settings-retrieval-grid">
            <div className="settings-detail-row">
              <SearchIcon className="settings-row-icon" aria-hidden="true" />
              <div>
                <h3>{t("keyword")}</h3>
                <p>{t("settingsKeywordWhy")}</p>
              </div>
              <SettingsStatus state={overview?.databaseState ?? "unknown"} />
            </div>
            <div className="settings-detail-row">
              <NetworkIcon className="settings-row-icon" aria-hidden="true" />
              <div>
                <h3>{t("semantic")}</h3>
                <p>{t("settingsSemanticWhy")}</p>
              </div>
              <SettingsStatus state={overview?.semanticState ?? "unknown"} />
              <a className="settings-text-link" href="#inference">
                {t("settingsConfigureSearchModel")}{" "}
                <ChevronRightIcon aria-hidden="true" />
              </a>
            </div>
            <div className="settings-detail-row">
              <FileTextIcon className="settings-row-icon" aria-hidden="true" />
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
        </SettingsSection>
        <InferenceSettings onChanged={resource.refresh} />
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
              <p>{t("settingsConnectStep2")}</p>
              <p className="settings-inline-note">
                {t("settingsHeaderName")} <code>X-ContextDepot-Key</code>
              </p>
              {endpoint && (
                <Collapsible className="settings-connection-example">
                  <CollapsibleTrigger asChild>
                    <Button
                      type="button"
                      variant="ghost"
                      size="sm"
                      className="settings-disclosure-trigger"
                    >
                      {t("settingsConnectionExample")}
                      <ChevronDownIcon
                        data-icon="inline-end"
                        aria-hidden="true"
                      />
                    </Button>
                  </CollapsibleTrigger>
                  <CollapsibleContent>
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
                  </CollapsibleContent>
                </Collapsible>
              )}
            </div>
            <Collapsible className="settings-connection-help">
              <CollapsibleTrigger asChild>
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  className="settings-disclosure-trigger"
                >
                  {t("help")}
                  <ChevronDownIcon data-icon="inline-end" aria-hidden="true" />
                </Button>
              </CollapsibleTrigger>
              <CollapsibleContent>
                <ol>
                  <li>{t("settingsConnectStep1")}</li>
                  <li>{t("settingsConnectStep2")}</li>
                  <li>{t("settingsConnectStep3")}</li>
                </ol>
              </CollapsibleContent>
            </Collapsible>
            <a
              className="settings-integration-guide"
              href="https://github.com/danvic712/context-depot#3-connect-your-agent"
              target="_blank"
              rel="noreferrer"
            >
              {t("settingsIntegrationGuide")}{" "}
              <ExternalLinkIcon size={13} aria-hidden="true" />
            </a>
          </div>
        </SettingsSection>
        <AboutSettings
          version={meta.data?.version}
          versionPending={meta.pending}
          versionFailed={!!meta.error}
          depotName={overview?.depotName}
          depotPending={resource.pending}
        />
      </div>
    </div>
  );
}
