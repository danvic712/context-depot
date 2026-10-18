import { PencilIcon, PlugIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";
import {
  Item,
  ItemGroup,
  ItemMedia,
  ItemContent,
  ItemTitle,
  ItemDescription,
  ItemActions,
} from "@/components/ui/item";
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
  if (!providers.length) return null;
  return (
    <div className="settings-inference-connections">
      <h3>{t("settingsConnectedProviders")}</h3>
      <ItemGroup className="settings-provider-list">
        {providers.map((provider) => {
          const routes = settings.routes.filter(
            (route) => route.providerId === provider.id,
          );
          return (
            <Item
              role="listitem"
              className="settings-provider"
              key={provider.id}
            >
              <ItemMedia variant="icon">
                <PlugIcon aria-hidden="true" />
              </ItemMedia>
              <ItemContent className="settings-provider-copy">
                <ItemTitle className="settings-provider-name">
                  <h4>{provider.name}</h4>
                  {(!provider.hasApiKey || !provider.endpoint) && (
                    <SettingsStatus state="unconfigured" />
                  )}
                </ItemTitle>
                <code>
                  {provider.endpoint ?? t("settingsProviderEndpointMissing")}
                </code>
                <ItemDescription className="line-clamp-none">
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
                </ItemDescription>
              </ItemContent>
              <ItemActions>
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
              </ItemActions>
            </Item>
          );
        })}
      </ItemGroup>
    </div>
  );
}
