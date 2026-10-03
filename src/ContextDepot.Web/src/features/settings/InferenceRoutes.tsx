import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";
import { SettingsStatus } from "./SettingsStatus";
import type { InferenceProviderSettings, InferenceRoute } from "./settings-api";

export function InferenceRoutes({
  settings,
  disabled,
  onConfigure,
}: {
  settings: InferenceProviderSettings;
  disabled: boolean;
  onConfigure: (route: InferenceRoute) => void;
}) {
  const { t } = useTranslation();
  return (
    <div className="settings-inference-routes">
      {settings.routes.map((route) => (
        <article key={route.capability} className="settings-inference-route">
          <div className="settings-inference-route-heading">
            <h3>{route.capability === "embedding" ? "Embedding" : "Chat"}</h3>
            <SettingsStatus state={route.runtimeState} />
          </div>
          <p>
            {t(
              route.capability === "embedding"
                ? "settingsEmbeddingWhy"
                : "settingsChatWhy",
            )}
          </p>
          <dl>
            <div>
              <dt>Provider</dt>
              <dd>{route.providerName ?? t("settingsState_unconfigured")}</dd>
            </div>
            <div>
              <dt>{t("settingsModel")}</dt>
              <dd>{route.model ?? "—"}</dd>
            </div>
          </dl>
          <Button
            variant="outline"
            size="sm"
            disabled={disabled}
            onClick={() => onConfigure(route)}
          >
            {t("settingsConfigureCapability", {
              capability:
                route.capability === "embedding" ? "Embedding" : "Chat",
            })}
          </Button>
        </article>
      ))}
    </div>
  );
}
