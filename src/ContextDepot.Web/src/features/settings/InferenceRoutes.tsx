import { useTranslation } from "react-i18next";
import { MessageSquareIcon, ScanSearchIcon } from "lucide-react";
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
      {[...settings.routes]
        .sort((a, b) => b.capability.localeCompare(a.capability))
        .map((route) => (
          <article key={route.capability} className="settings-inference-route">
            <div className="settings-inference-route-heading">
              <h3>
                {route.capability === "embedding" ? (
                  <ScanSearchIcon aria-hidden="true" />
                ) : (
                  <MessageSquareIcon aria-hidden="true" />
                )}
                {t(
                  route.capability === "embedding"
                    ? "settingsEmbeddingTitle"
                    : "settingsChatTitle",
                )}
              </h3>
              {route.capability === "chat" && !route.providerId ? (
                <span className="settings-inference-optional">
                  {t("settingsOptional")}
                </span>
              ) : (
                <SettingsStatus state={route.runtimeState} />
              )}
            </div>
            <p>
              {t(
                route.capability === "embedding"
                  ? "settingsEmbeddingWhy"
                  : "settingsChatWhy",
              )}
            </p>
            {route.providerId ? (
              <dl>
                <div>
                  <dt>{t("settingsProviderType")}</dt>
                  <dd>
                    {route.providerName ?? t("settingsState_unconfigured")}
                  </dd>
                </div>
                <div>
                  <dt>{t("settingsModel")}</dt>
                  <dd>
                    <code>{route.model ?? "—"}</code>
                  </dd>
                </div>
              </dl>
            ) : (
              <p className="settings-inference-placeholder">
                {t(
                  route.capability === "embedding"
                    ? "settingsEmbeddingEmpty"
                    : "settingsChatEmpty",
                )}
              </p>
            )}
            {route.runtimeState === "pending" && (
              <p className="settings-inline-note" role="status">
                {t("settingsInferencePending")}
              </p>
            )}
            <Button
              variant="outline"
              size="sm"
              disabled={disabled}
              onClick={() => onConfigure(route)}
            >
              {t("settingsConfigureCapability", {
                capability: t(
                  route.capability === "embedding"
                    ? "settingsEmbeddingTitle"
                    : "settingsChatTitle",
                ),
              })}
            </Button>
          </article>
        ))}
    </div>
  );
}
