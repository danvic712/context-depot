import { useState } from "react";
import { KeyRoundIcon, PlusIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import { Button } from "@/components/ui/button";
import {
  Empty,
  EmptyHeader,
  EmptyMedia,
  EmptyTitle,
  EmptyDescription,
} from "@/components/ui/empty";
import { SettingsSection } from "@/pages/settings/SettingsSection";
import { SettingsResourceState } from "./SettingsResourceState";
import { AccessKeyCard } from "./AccessKeyCard";
import { getAccessKeys, type AccessKey } from "./settings-api";
import { useSettingsResource } from "./use-settings-resource";
import { AccessKeyDialog, type KeyAction } from "./AccessKeyDialog";
import { SettingsRefreshButton } from "./SettingsConflictRecovery";

export function AccessKeySettings() {
  const { t } = useTranslation();
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
        <div className="flex flex-wrap gap-2">
          <SettingsRefreshButton
            pending={resource.pending}
            onRefresh={resource.refresh}
          />
          <Button
            size="sm"
            disabled={!resource.data || !!resource.error || resource.pending}
            onClick={() => open("create")}
          >
            <PlusIcon aria-hidden="true" />
            {t("settingsCreateKey")}
          </Button>
        </div>
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
            <Empty className="settings-empty border">
              <EmptyHeader>
                <EmptyMedia variant="icon">
                  <KeyRoundIcon aria-hidden="true" />
                </EmptyMedia>
                <EmptyTitle>{t("settingsNoKeys")}</EmptyTitle>
                <EmptyDescription>{t("settingsNoKeysWhy")}</EmptyDescription>
              </EmptyHeader>
            </Empty>
          )}
          <div className="settings-key-list">
            {items.map((key) => (
              <AccessKeyCard
                key={key.id}
                accessKey={key}
                workspaces={workspaces}
                disabled={resource.pending || !!resource.error}
                onAction={open}
              />
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
