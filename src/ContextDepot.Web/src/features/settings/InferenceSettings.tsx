import { useState } from "react";
import { PlusIcon, SlidersHorizontalIcon } from "lucide-react";
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
import { InferenceProviderDialog } from "./InferenceProviderDialog";
import { InferenceRoutes } from "./InferenceRoutes";
import { InferenceRouteDialog } from "./InferenceRouteDialog";
import { InferenceConnections } from "./InferenceConnections";
import { availableInferenceProviders } from "./inference-connections";

export function InferenceSettings({ onChanged }: { onChanged: () => void }) {
  const { t } = useTranslation();
  const resource = useSettingsResource(getInferenceProviders, true);
  const [editing, setEditing] = useState<InferenceProvider | "new">();
  const [editingRoute, setEditingRoute] = useState<InferenceRoute>();
  const [connectingCapability, setConnectingCapability] =
    useState<InferenceRoute["capability"]>();
  function connect(capability?: InferenceRoute["capability"]) {
    setEditingRoute(undefined);
    setConnectingCapability(capability);
    setEditing("new");
  }
  function saved() {
    resource.refresh();
    onChanged();
  }
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
          onClick={() => connect()}
        >
          <PlusIcon aria-hidden="true" />
          {t("settingsConnectProvider")}
        </Button>
      }
    >
      <SettingsResourceState resource={resource} onRetry={resource.refresh} />
      {resource.data && (
        <>
          <InferenceRoutes
            settings={resource.data}
            disabled={resource.pending || !!resource.error}
            onConfigure={(route) => {
              if (
                route.providerId ||
                availableInferenceProviders(resource.data!, route.capability)
                  .length
              )
                setEditingRoute(route);
              else connect(route.capability);
            }}
          />
          <InferenceConnections
            settings={resource.data}
            disabled={resource.pending || !!resource.error}
            onEdit={setEditing}
          />
        </>
      )}
      {editingRoute && resource.data && (
        <InferenceRouteDialog
          route={editingRoute}
          settings={resource.data}
          onClose={() => setEditingRoute(undefined)}
          onConnect={() => connect(editingRoute.capability)}
          onSaved={saved}
        />
      )}
      {editing && resource.data && (
        <InferenceProviderDialog
          provider={editing === "new" ? undefined : editing}
          capability={editing === "new" ? connectingCapability : undefined}
          settings={resource.data}
          onClose={() => setEditing(undefined)}
          onSaved={saved}
        />
      )}
    </SettingsSection>
  );
}
