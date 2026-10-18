import { useId, useState } from "react";
import {
  KeyRoundIcon,
  PencilIcon,
  RotateCwIcon,
  BanIcon,
  InfoIcon,
  ChevronDownIcon,
} from "lucide-react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import {
  Card,
  CardHeader,
  CardTitle,
  CardContent,
  CardFooter,
} from "@/components/ui/card";
import {
  Collapsible,
  CollapsibleTrigger,
  CollapsibleContent,
} from "@/components/ui/collapsible";
import {
  Popover,
  PopoverTrigger,
  PopoverContent,
} from "@/components/ui/popover";
import { SettingsStatus } from "./SettingsStatus";
import type { AccessKey, KeyWorkspace } from "./settings-api";
import type { KeyAction } from "./AccessKeyDialog";

export function AccessKeyCard({
  accessKey,
  workspaces,
  disabled,
  onAction,
}: {
  accessKey: AccessKey;
  workspaces: KeyWorkspace[];
  disabled: boolean;
  onAction: (action: KeyAction, key: AccessKey) => void;
}) {
  const { t, i18n } = useTranslation();
  const [showAllSpaces, setShowAllSpaces] = useState(false);
  const scopeId = useId();
  function spaceTag(id: string) {
    const workspace = workspaces.find((item) => item.id === id);
    return (
      <li key={id} title={workspace?.path}>
        <Badge
          variant="secondary"
          className="max-w-full whitespace-normal break-words"
        >
          {workspace?.name ?? t("settingsMissingWorkspace")}
        </Badge>
      </li>
    );
  }
  const date = (value: string) =>
    new Intl.DateTimeFormat(i18n.language, {
      dateStyle: "medium",
      timeStyle: "short",
    }).format(new Date(value));
  return (
    <Card
      role="article"
      aria-labelledby={`${scopeId}-name`}
      className="settings-key-card"
      data-revoked={!!accessKey.revokedAt || undefined}
    >
      <CardHeader className="settings-key-heading p-0">
        <span className="settings-key-icon">
          <KeyRoundIcon aria-hidden="true" />
        </span>
        <CardTitle className="settings-key-identity">
          <h3 id={`${scopeId}-name`}>{accessKey.name}</h3>
        </CardTitle>
        <SettingsStatus state={accessKey.revokedAt ? "revoked" : "active"} />
        <div className="settings-key-identifier">
          <span className="settings-key-identifier-label">
            {t("settingsKeyIdentifier")}
            {!accessKey.revokedAt && (
              <Popover>
                <PopoverTrigger asChild>
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    className="settings-key-help"
                    aria-label={t("settingsKeyHelp")}
                    title={t("settingsKeyHelp")}
                  >
                    <InfoIcon aria-hidden="true" />
                  </Button>
                </PopoverTrigger>
                <PopoverContent
                  className="settings-key-help-content"
                  align="start"
                  side="top"
                  collisionPadding={16}
                  aria-labelledby={`${scopeId}-help`}
                >
                  <h3 id={`${scopeId}-help`}>{t("settingsKeyHelp")}</h3>
                  <p>{t("settingsKeyHiddenWhy")}</p>
                </PopoverContent>
              </Popover>
            )}
          </span>
          <code>
            {accessKey.prefix}
            <span aria-hidden="true">.••••</span>
          </code>
        </div>
      </CardHeader>
      <Collapsible open={showAllSpaces} onOpenChange={setShowAllSpaces} asChild>
        <CardContent className="settings-key-scope p-0">
          <h4>
            {t(
              accessKey.revokedAt
                ? "settingsPreviouslyGrantedSpaces"
                : "settingsGrantedWorkspaces",
            )}
            <span className="settings-space-count">
              {t("settingsSpaceCount", {
                count: accessKey.workspaceIds.length,
              })}
            </span>
          </h4>
          {accessKey.workspaceIds.length ? (
            <ul className="settings-grant-tags">
              {accessKey.workspaceIds.slice(0, 3).map(spaceTag)}
            </ul>
          ) : (
            <p>{t("settingsNoGrantedSpaces")}</p>
          )}
          {accessKey.workspaceIds.length > 3 && (
            <>
              <CollapsibleContent className="settings-extra-spaces">
                <ul className="settings-grant-tags">
                  {accessKey.workspaceIds.slice(3).map(spaceTag)}
                </ul>
              </CollapsibleContent>
              <CollapsibleTrigger asChild>
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  className="settings-show-spaces"
                >
                  {t(
                    showAllSpaces
                      ? "settingsFewerSpaces"
                      : "settingsMoreSpaces",
                    {
                      count: accessKey.workspaceIds.length - 3,
                    },
                  )}
                  <ChevronDownIcon aria-hidden="true" />
                </Button>
              </CollapsibleTrigger>
            </>
          )}
          {!accessKey.revokedAt && (
            <p className="settings-key-permission">
              {t(
                accessKey.canCreateRootWorkspaces
                  ? "settingsCreationEnabled"
                  : "settingsCreationDisabled",
              )}
            </p>
          )}
        </CardContent>
      </Collapsible>
      <CardContent className="settings-key-history p-0">
        <dl className="settings-key-dates">
          <div>
            <dt>{t("settingsKeyCreated")}</dt>
            <dd>
              <time dateTime={accessKey.createdAt}>
                {date(accessKey.createdAt)}
              </time>
            </dd>
          </div>
          <div>
            <dt>{t("settingsLastUsed")}</dt>
            <dd>
              {accessKey.lastUsedAt ? (
                <time dateTime={accessKey.lastUsedAt}>
                  {date(accessKey.lastUsedAt)}
                </time>
              ) : (
                t("settingsNeverUsed")
              )}
            </dd>
          </div>
          {accessKey.revokedAt && (
            <div>
              <dt>{t("settingsKeyDisabledAt")}</dt>
              <dd>
                <time dateTime={accessKey.revokedAt}>
                  {date(accessKey.revokedAt)}
                </time>
              </dd>
            </div>
          )}
        </dl>
      </CardContent>
      {!accessKey.revokedAt && (
        <CardFooter className="settings-key-actions px-0">
          <Button
            variant="outline"
            size="sm"
            disabled={disabled}
            onClick={() => onAction("grants", accessKey)}
            aria-label={t("settingsEditGrantsFor", { name: accessKey.name })}
          >
            <PencilIcon aria-hidden="true" />
            {t("settingsEditGrants")}
          </Button>
          <Button
            variant="ghost"
            size="sm"
            disabled={disabled}
            onClick={() => onAction("rotate", accessKey)}
            aria-label={t("settingsRotateFor", { name: accessKey.name })}
          >
            <RotateCwIcon aria-hidden="true" />
            {t("settingsRotate")}
          </Button>
          <Button
            variant="ghost"
            size="sm"
            className="settings-danger"
            disabled={disabled}
            onClick={() => onAction("revoke", accessKey)}
            aria-label={t("settingsRevokeFor", { name: accessKey.name })}
          >
            <BanIcon aria-hidden="true" />
            {t("settingsRevoke")}
          </Button>
        </CardFooter>
      )}
    </Card>
  );
}
