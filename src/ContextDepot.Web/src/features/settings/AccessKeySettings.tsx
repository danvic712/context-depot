import { useState } from "react";
import {
  KeyRoundIcon,
  PlusIcon,
  PencilIcon,
  RotateCwIcon,
  Trash2Icon,
} from "lucide-react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { Button } from "@/components/ui/button";
import { SettingsSection } from "@/pages/settings/SettingsSection";
import { SettingsResourceState } from "./SettingsResourceState";
import { SettingsStatus } from "./SettingsStatus";
import { getAccessKeys, type AccessKey } from "./settings-api";
import { useSettingsResource } from "./use-settings-resource";
import { AccessKeyDialog, type KeyAction } from "./AccessKeyDialog";

export function AccessKeySettings() {
  const { t, i18n } = useTranslation();
  const resource = useSettingsResource(getAccessKeys);
  const [action, setAction] = useState<KeyAction>();
  const [selected, setSelected] = useState<AccessKey>();
  const [showRevoked, setShowRevoked] = useState(false);
  function open(next: KeyAction, key?: AccessKey) {
    setSelected(key);
    setAction(next);
  }
  const active = resource.data?.items.filter((key) => !key.revokedAt) ?? [];
  const revoked = resource.data?.items.filter((key) => !!key.revokedAt) ?? [];
  const workspaces = resource.data?.workspaces ?? [];
  const items = showRevoked ? (resource.data?.items ?? []) : active;
  return (
    <SettingsSection
      id="access-keys"
      title={t("settingsKeysTitle")}
      detail={t("settingsKeysWhy")}
      icon={KeyRoundIcon}
      action={
        <Button
          size="sm"
          disabled={
            !resource.data ||
            !!resource.error ||
            resource.pending ||
            !workspaces.length
          }
          onClick={() => open("create")}
        >
          <PlusIcon aria-hidden="true" />
          {t("settingsCreateKey")}
        </Button>
      }
    >
      <SettingsResourceState resource={resource} onRetry={resource.refresh} />
      {resource.data && (
        <>
          {!workspaces.length && (
            <p className="settings-inline-note">
              {t("settingsNoWorkspaces")}{" "}
              <Link to="/spaces">{t("spaces")}</Link>
            </p>
          )}
          {!items.length && (
            <div className="settings-empty">
              <KeyRoundIcon aria-hidden="true" />
              <div>
                <h3>{t("settingsNoKeys")}</h3>
                <p>{t("settingsNoKeysWhy")}</p>
              </div>
            </div>
          )}
          <div className="settings-key-list">
            {items.map((key) => (
              <div
                className="settings-key-row"
                key={key.id}
                data-revoked={!!key.revokedAt || undefined}
              >
                <div className="settings-key-copy">
                  <div className="settings-key-name">
                    <h3>{key.name}</h3>
                    <SettingsStatus
                      state={key.revokedAt ? "revoked" : "active"}
                    />
                  </div>
                  <code>{key.prefix}.••••••••</code>
                  <p>
                    {t("settingsLastUsed")}:{" "}
                    {key.lastUsedAt
                      ? new Intl.DateTimeFormat(i18n.language, {
                          dateStyle: "medium",
                          timeStyle: "short",
                        }).format(new Date(key.lastUsedAt))
                      : t("settingsNeverUsed")}
                  </p>
                  <div className="settings-grant-tags">
                    {key.workspaceIds.map((id) => (
                      <span key={id}>
                        {workspaces.find((workspace) => workspace.id === id)
                          ?.path ?? t("settingsMissingWorkspace")}
                      </span>
                    ))}
                  </div>
                </div>
                {!key.revokedAt && (
                  <div className="settings-key-actions">
                    <Button
                      variant="outline"
                      size="sm"
                      disabled={resource.pending || !!resource.error}
                      onClick={() => open("grants", key)}
                      aria-label={t("settingsEditGrantsFor", {
                        name: key.name,
                      })}
                    >
                      <PencilIcon aria-hidden="true" />
                      {t("settingsEditGrants")}
                    </Button>
                    <Button
                      variant="outline"
                      size="icon-sm"
                      disabled={resource.pending || !!resource.error}
                      onClick={() => open("rotate", key)}
                      aria-label={t("settingsRotateFor", { name: key.name })}
                      title={t("settingsRotate")}
                    >
                      <RotateCwIcon aria-hidden="true" />
                    </Button>
                    <Button
                      variant="outline"
                      size="icon-sm"
                      className="settings-danger"
                      disabled={resource.pending || !!resource.error}
                      onClick={() => open("revoke", key)}
                      aria-label={t("settingsRevokeFor", { name: key.name })}
                      title={t("settingsRevoke")}
                    >
                      <Trash2Icon aria-hidden="true" />
                    </Button>
                  </div>
                )}
              </div>
            ))}
          </div>
          {revoked.length > 0 && (
            <Button
              variant="ghost"
              size="sm"
              onClick={() => setShowRevoked((value) => !value)}
              aria-expanded={showRevoked}
            >
              {t(showRevoked ? "settingsHideRevoked" : "settingsShowRevoked", {
                count: revoked.length,
              })}
            </Button>
          )}
        </>
      )}
      {action && (
        <AccessKeyDialog
          action={action}
          accessKey={selected}
          workspaces={workspaces}
          onClose={() => setAction(undefined)}
          onChanged={resource.refresh}
        />
      )}
    </SettingsSection>
  );
}
