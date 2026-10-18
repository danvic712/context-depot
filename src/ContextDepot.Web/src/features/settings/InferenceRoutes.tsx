import { useTranslation } from "react-i18next";
import { MessageSquareIcon, ScanSearchIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import {
  Card,
  CardHeader,
  CardTitle,
  CardDescription,
  CardContent,
  CardFooter,
} from "@/components/ui/card";
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
          <Card
            role="article"
            key={route.capability}
            className="settings-inference-route"
          >
            <CardHeader className="p-0">
              <div className="settings-inference-route-heading">
                <CardTitle>
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
                </CardTitle>
                {route.capability === "chat" && !route.providerId ? (
                  <Badge variant="secondary">{t("settingsOptional")}</Badge>
                ) : (
                  <SettingsStatus state={route.runtimeState} />
                )}
              </div>
              <CardDescription>
                {t(
                  route.capability === "embedding"
                    ? "settingsEmbeddingWhy"
                    : "settingsChatWhy",
                )}
              </CardDescription>
            </CardHeader>
            <CardContent className="p-0">
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
              ) : null}
              {route.runtimeState === "pending" && (
                <p className="settings-inline-note" role="status">
                  {t("settingsInferencePending")}
                </p>
              )}
            </CardContent>
            <CardFooter className="mt-auto p-0">
              <Button
                variant={
                  route.capability === "embedding" ? "default" : "outline"
                }
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
            </CardFooter>
          </Card>
        ))}
    </div>
  );
}
