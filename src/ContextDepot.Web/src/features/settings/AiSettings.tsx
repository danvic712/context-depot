import { useState } from "react";
import { BotIcon, PencilIcon, SlidersHorizontalIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";
import { SettingsSection } from "@/pages/settings/SettingsSection";
import { getAiSettings, type AiRoute } from "./settings-api";
import { useSettingsResource } from "./use-settings-resource";
import { SettingsResourceState } from "./SettingsResourceState";
import { SettingsStatus } from "./SettingsStatus";
import { AiRouteDialog } from "./AiRouteDialog";

export function AiSettings({ onChanged }: { onChanged: () => void }) {
  const { t } = useTranslation();
  const resource = useSettingsResource(getAiSettings, true);
  const [editing, setEditing] = useState<AiRoute>();
  return (
    <SettingsSection
      id="ai"
      title={t("settingsAiTitle")}
      detail={t("settingsAiWhy")}
      icon={SlidersHorizontalIcon}
    >
      <SettingsResourceState resource={resource} onRetry={resource.refresh} />
      {resource.data && (
        <div className="settings-ai-grid">
          {["embedding", "chat"].map((capability) => {
            const route = resource.data?.find(
              (item) => item.capability === capability,
            );
            if (!route) return null;
            return (
              <article className="settings-ai-route" key={route.capability}>
                <div className="settings-ai-heading">
                  <div>
                    <h3>
                      {t(
                        route.capability === "embedding"
                          ? "settingsEmbeddingTitle"
                          : "settingsChatTitle",
                      )}
                    </h3>
                    <p>
                      {t(
                        route.capability === "embedding"
                          ? "settingsEmbeddingWhy"
                          : "settingsChatWhy",
                      )}
                    </p>
                  </div>
                  <SettingsStatus state={route.runtimeState} />
                </div>
                {route.model ? (
                  <dl className="settings-definition-list">
                    <div>
                      <dt>{t("settingsProvider")}</dt>
                      <dd>{route.providerName}</dd>
                    </div>
                    <div>
                      <dt>{t("settingsModel")}</dt>
                      <dd>
                        <code>{route.model}</code>
                      </dd>
                    </div>
                    <div>
                      <dt>{t("settingsAiEndpoint")}</dt>
                      <dd>
                        <code>{route.endpoint}</code>
                      </dd>
                    </div>
                    {route.dimensions !== null && (
                      <div>
                        <dt>{t("settingsDimensions")}</dt>
                        <dd>{route.dimensions.toLocaleString()}</dd>
                      </div>
                    )}
                    <div>
                      <dt>{t("settingsTimeout")}</dt>
                      <dd>
                        {t("settingsSeconds", { count: route.timeoutSeconds })}
                      </dd>
                    </div>
                    <div>
                      <dt>API Key</dt>
                      <dd>
                        {t(
                          route.hasApiKey
                            ? "settingsApiKeyStored"
                            : "settingsApiKeyMissing",
                        )}
                      </dd>
                    </div>
                  </dl>
                ) : (
                  <div className="settings-ai-empty">
                    <BotIcon aria-hidden="true" />
                    <p>{t("settingsAiUnconfigured")}</p>
                  </div>
                )}
                {route.runtimeState === "pending" && (
                  <p className="settings-inline-note" role="status">
                    {t("settingsAiPending")}
                  </p>
                )}
                <Button
                  variant="outline"
                  size="sm"
                  disabled={resource.pending || !!resource.error}
                  onClick={() => setEditing(route)}
                >
                  <PencilIcon aria-hidden="true" />
                  {t(route.model ? "settingsEditAi" : "settingsConfigureAi")}
                </Button>
              </article>
            );
          })}
        </div>
      )}
      {editing && (
        <AiRouteDialog
          route={editing}
          onClose={() => setEditing(undefined)}
          onSaved={() => {
            resource.refresh();
            onChanged();
          }}
        />
      )}
    </SettingsSection>
  );
}
