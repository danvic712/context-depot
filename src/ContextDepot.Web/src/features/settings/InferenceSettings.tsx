import { useState } from "react";
import {
  PencilIcon,
  PlusIcon,
  PlugIcon,
  SlidersHorizontalIcon,
} from "lucide-react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";
import { SettingsSection } from "@/pages/settings/SettingsSection";
import {
  getInferenceProviders,
  type InferenceProvider,
  type InferenceRoute,
} from "./settings-api";
import { useSettingsResource } from "./use-settings-resource";
import { SettingsResourceState } from "./SettingsResourceState";
import { SettingsStatus } from "./SettingsStatus";
import { InferenceProviderDialog } from "./InferenceProviderDialog";
import { InferenceRoutes } from "./InferenceRoutes";
import { InferenceRouteDialog } from "./InferenceRouteDialog";

export function InferenceSettings({ onChanged }: { onChanged: () => void }) {
  const { t } = useTranslation();
  const resource = useSettingsResource(getInferenceProviders, true);
  const [editing, setEditing] = useState<InferenceProvider | "new">();
  const [editingRoute, setEditingRoute] = useState<InferenceRoute>();
  const providers = resource.data?.providers ?? [];
  const routes = resource.data?.routes ?? [];
  return (
    <SettingsSection
      id="inference"
      title={t("settingsInferenceTitle")}
      detail={t("settingsInferenceWhy")}
      icon={SlidersHorizontalIcon}
      action={
        <Button
          variant="outline"
          size="sm"
          disabled={!resource.data || resource.pending || !!resource.error}
          onClick={() => setEditing("new")}
        >
          <PlusIcon aria-hidden="true" />
          {t("settingsAddProvider")}
        </Button>
      }
    >
      <SettingsResourceState resource={resource} onRetry={resource.refresh} />
      {resource.data && (
        <>
          <InferenceRoutes
            settings={resource.data}
            disabled={resource.pending || !!resource.error}
            onConfigure={setEditingRoute}
          />
          {!providers.length && (
            <div className="settings-empty">
              <PlugIcon aria-hidden="true" />
              <div>
                <h3>{t("settingsNoProviders")}</h3>
                <p>{t("settingsNoProvidersWhy")}</p>
              </div>
            </div>
          )}
          {routes.some((route) => !route.providerId) &&
            providers.length > 0 && (
              <p className="settings-provider-note">
                {t("settingsModelsMissing")}
              </p>
            )}
          <div className="settings-provider-list">
            {providers.map((provider) => {
              const models = routes.filter(
                (route) => route.providerId === provider.id,
              );
              return (
                <article className="settings-provider" key={provider.id}>
                  <div className="settings-provider-heading">
                    <span className="settings-provider-icon">
                      <PlugIcon aria-hidden="true" />
                    </span>
                    <div>
                      <h3>{provider.name}</h3>
                      <code>
                        {provider.endpoint ??
                          t("settingsProviderEndpointMissing")}
                      </code>
                    </div>
                    <Button
                      variant="outline"
                      size="sm"
                      aria-label={t(
                        provider.hasApiKey
                          ? "settingsEditProviderFor"
                          : "settingsConfigureProviderFor",
                        {
                          name: provider.name,
                        },
                      )}
                      disabled={resource.pending || !!resource.error}
                      onClick={() => setEditing(provider)}
                    >
                      <PencilIcon aria-hidden="true" />
                      {t(
                        provider.hasApiKey
                          ? "settingsEditProvider"
                          : "settingsConfigureProvider",
                      )}
                    </Button>
                  </div>
                  <div className="settings-provider-meta">
                    <span>
                      {provider.kind === "custom"
                        ? t("settingsProviderCustom")
                        : resource.data?.presets.find(
                            (preset) => preset.kind === provider.kind,
                          )?.name}
                    </span>
                    <SettingsStatus
                      state={
                        provider.hasApiKey && provider.endpoint
                          ? "configured"
                          : "unconfigured"
                      }
                    />
                    <span>
                      {t(
                        provider.hasApiKey
                          ? "settingsApiKeyStored"
                          : "settingsApiKeyMissing",
                      )}
                    </span>
                  </div>
                  {models.length ? (
                    <div className="settings-provider-models">
                      {models.map((route) => (
                        <div
                          className="settings-provider-model"
                          key={route.capability}
                        >
                          <div>
                            <span>
                              {t(
                                route.capability === "embedding"
                                  ? "settingsEmbeddingTitle"
                                  : "settingsChatTitle",
                              )}
                            </span>
                            <strong>
                              <code>{route.model}</code>
                            </strong>
                            <p>
                              {route.dimensions !== null && (
                                <>
                                  {t("settingsDimensions")}:{" "}
                                  {route.dimensions.toLocaleString()} ·{" "}
                                </>
                              )}
                              {t("settingsSeconds", {
                                count: route.timeoutSeconds,
                              })}
                            </p>
                          </div>
                          <SettingsStatus state={route.runtimeState} />
                        </div>
                      ))}
                    </div>
                  ) : (
                    <p className="settings-provider-note">
                      {t("settingsProviderUnused")}
                    </p>
                  )}
                  {models.some((route) => route.runtimeState === "pending") && (
                    <p className="settings-inline-note" role="status">
                      {t("settingsInferencePending")}
                    </p>
                  )}
                </article>
              );
            })}
          </div>
        </>
      )}
      {editingRoute && resource.data && (
        <InferenceRouteDialog
          route={editingRoute}
          settings={resource.data}
          onClose={() => setEditingRoute(undefined)}
          onSaved={() => {
            resource.refresh();
            onChanged();
          }}
        />
      )}
      {editing && resource.data && (
        <InferenceProviderDialog
          provider={editing === "new" ? undefined : editing}
          settings={resource.data}
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
