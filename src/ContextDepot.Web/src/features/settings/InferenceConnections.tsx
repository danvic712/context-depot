import { PencilIcon, PlugIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";
import { SettingsStatus } from "./SettingsStatus";
import { connectedInferenceProviders } from "./inference-connections";
import type {
  InferenceProvider,
  InferenceProviderSettings,
} from "./settings-api";

export function InferenceConnections({
  settings,
  disabled,
  onEdit,
}: {
  settings: InferenceProviderSettings;
  disabled: boolean;
  onEdit: (provider: InferenceProvider) => void;
}) {
  const { t } = useTranslation();
  const providers = connectedInferenceProviders(settings);
  if (!providers.length)
    return (
      <div className="settings-inference-empty">
        <PlugIcon aria-hidden="true" />
        <p>{t("settingsNoConnectionsWhy")}</p>
      </div>
    );
  return (
    <div className="settings-inference-connections">
      <h3>{t("settingsConnectedProviders")}</h3>
      <div className="settings-provider-list">
        {providers.map((provider) => {
          const routes = settings.routes.filter(
            (route) => route.providerId === provider.id,
          );
          return (
            <article className="settings-provider" key={provider.id}>
              <span className="settings-provider-icon">
                <PlugIcon aria-hidden="true" />
              </span>
              <div className="settings-provider-copy">
                <div className="settings-provider-name">
                  <h4>{provider.name}</h4>
                  {(!provider.hasApiKey || !provider.endpoint) && (
                    <SettingsStatus state="unconfigured" />
                  )}
                </div>
                <code>
                  {provider.endpoint ?? t("settingsProviderEndpointMissing")}
                </code>
                <p>
                  {t(
                    provider.hasApiKey
                      ? "settingsApiKeyStored"
                      : "settingsApiKeyMissing",
                  )}
                  <span aria-hidden="true"> · </span>
                  {routes.length
                    ? routes
                        .map((route) =>
                          t(
                            route.capability === "embedding"
                              ? "settingsEmbeddingTitle"
                              : "settingsChatTitle",
                          ),
                        )
                        .join(" / ")
                    : t("settingsProviderUnused")}
                </p>
              </div>
              <Button
                variant="outline"
                size="sm"
                aria-label={t("settingsEditProviderFor", {
                  name: provider.name,
                })}
                disabled={disabled}
                onClick={() => onEdit(provider)}
              >
                <PencilIcon aria-hidden="true" />
                {t("settingsEditConnection")}
              </Button>
            </article>
          );
        })}
      </div>
    </div>
  );
}
