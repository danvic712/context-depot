import { useState, type ReactNode } from "react";
import { PlusIcon, SlidersHorizontalIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";
import { SettingsSection } from "@/pages/settings/SettingsSection";
import {
  type InferenceProvider,
  type InferenceProviderSettings,
  type InferenceRoute,
} from "./settings-api";
import { useSettingsResource } from "./use-settings-resource";
import { SettingsResourceState } from "./SettingsResourceState";
import { InferenceProviderDialog } from "./InferenceProviderDialog";
import { InferenceRoutes } from "./InferenceRoutes";
import { InferenceRouteDialog } from "./InferenceRouteDialog";
import { InferenceConnections } from "./InferenceConnections";
import { availableInferenceProviders } from "./inference-connections";
import { SettingsRefreshButton } from "./SettingsConflictRecovery";

import {
  savedInferenceEditor,
  type InferenceSettingsEditor,
} from "./inference-settings-editor";

export function InferenceSettings({
  onChanged,
  editor = savedInferenceEditor,
  draftSettings,
  note,
}: {
  onChanged: () => void;
  editor?: InferenceSettingsEditor;
  draftSettings?: InferenceProviderSettings;
  note?: ReactNode;
}) {
  const { t } = useTranslation();
  const loadedResource = useSettingsResource(editor.load, !editor.isDraft);
  const resource =
    editor.isDraft && draftSettings
      ? {
          ...loadedResource,
          data: draftSettings,
          pending: false,
          error: undefined,
        }
      : loadedResource;
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
        <div className="flex flex-wrap gap-2">
          <SettingsRefreshButton
            pending={resource.pending}
            onRefresh={resource.refresh}
          />
          <Button
            variant="outline"
            size="sm"
            disabled={!resource.data || resource.pending || !!resource.error}
            onClick={() => connect()}
          >
            <PlusIcon aria-hidden="true" />
            {t("settingsConnectProvider")}
          </Button>
        </div>
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
          <div className="settings-inference-guidance">
            <InferenceConnections
              settings={resource.data}
              disabled={resource.pending || !!resource.error}
              onEdit={setEditing}
            />
            {note}
          </div>
        </>
      )}
      {editingRoute && resource.data && (
        <InferenceRouteDialog
          editor={editor}
          route={editingRoute}
          settings={resource.data}
          onClose={() => setEditingRoute(undefined)}
          onConnect={() => connect(editingRoute.capability)}
          onSaved={saved}
          onRefresh={resource.refresh}
        />
      )}
      {editing && resource.data && (
        <InferenceProviderDialog
          editor={editor}
          provider={editing === "new" ? undefined : editing}
          capability={editing === "new" ? connectingCapability : undefined}
          settings={resource.data}
          onClose={() => setEditing(undefined)}
          onSaved={saved}
          onRefresh={resource.refresh}
        />
      )}
    </SettingsSection>
  );
}
