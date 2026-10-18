import { useEffect, useRef, useState, type FormEvent } from "react";
import type { Messages } from "@/lib/i18n";
import { isAxiosError } from "axios";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { Link } from "react-router";
import {
  CheckIcon,
  InfoIcon,
  KeyRoundIcon,
  PlusIcon,
  RotateCwIcon,
  SaveIcon,
  Trash2Icon,
  XIcon,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Checkbox } from "@/components/ui/checkbox";
import { Dialog } from "@/components/ui/dialog";
import {
  Popover,
  PopoverTrigger,
  PopoverContent,
} from "@/components/ui/popover";
import {
  Field,
  FieldLabel,
  FieldSet,
  FieldLegend,
  FieldDescription,
  FieldContent,
} from "@/components/ui/field";
import { FormDialog } from "@/components/content/FormDialog";
import { SubmitButton } from "@/components/content/SubmitButton";
import { CopySetting } from "./SettingsStatus";
import { AccessKeyWorkspacePicker } from "./AccessKeyWorkspacePicker";
import {
  getAccessKeys,
  createAccessKey,
  rotateAccessKey,
  revokeAccessKey,
  updateAccessKeyGrants,
  type AccessKey,
  type KeyWorkspace,
  type IssuedKey,
} from "./settings-api";

import { SettingsConflictRecovery } from "./SettingsConflictRecovery";
import { useSettingsConflictRecovery } from "./use-settings-conflict-recovery";

export type KeyAction = "create" | "grants" | "rotate" | "revoke";
const titleKeys = {
  create: "settingsCreateKey",
  grants: "settingsEditGrants",
  rotate: "settingsRotate",
  revoke: "settingsRevoke",
} as const;
export function AccessKeyDialog({
  action,
  accessKey: initialKey,
  workspaces: initialWorkspaces,
  onClose,
  onChanged,
}: {
  action: KeyAction;
  accessKey?: AccessKey;
  workspaces: KeyWorkspace[];
  onClose: () => void;
  onChanged: () => void;
}) {
  const { t } = useTranslation();
  const [accessKey, setAccessKey] = useState(initialKey);
  const [workspaces, setWorkspaces] = useState(initialWorkspaces);
  const inactive = action !== "create" && (!accessKey || !!accessKey.revokedAt);
  const [name, setName] = useState("");
  const [grants, setGrants] = useState(accessKey?.workspaceIds ?? []);
  const [canCreateRootWorkspaces, setCanCreateRootWorkspaces] = useState(
    accessKey?.canCreateRootWorkspaces ?? false,
  );
  const [issued, setIssued] = useState<IssuedKey>();
  const [saving, setSaving] = useState(false);
  const recovery = useSettingsConflictRecovery(getAccessKeys);
  const pending = saving || recovery.pending;
  const [invalid, setInvalid] = useState<string[]>([]);
  const [failure, setFailure] = useState<keyof Messages>();
  const request = useRef<AbortController | null>(null);
  const form = useRef<HTMLFormElement>(null);
  const done = useRef<HTMLButtonElement>(null);
  useEffect(() => {
    if (issued) done.current?.focus();
  }, [issued]);
  useEffect(() => () => request.current?.abort(), []);
  const editsGrants = action === "create" || action === "grants";
  const ActionIcon =
    action === "create"
      ? PlusIcon
      : action === "grants"
        ? SaveIcon
        : action === "rotate"
          ? RotateCwIcon
          : Trash2Icon;
  async function submit(event: FormEvent) {
    event.preventDefault();
    if (pending || inactive || failure === "settingsConflict") return;
    const errors = [];
    if (action === "create" && (!name.trim() || name.trim().length > 200))
      errors.push("name");
    if (editsGrants && !grants.length && !canCreateRootWorkspaces)
      errors.push("grants");
    setInvalid(errors);
    setFailure(undefined);
    if (errors.length) {
      requestAnimationFrame(() =>
        form.current
          ?.querySelector<HTMLElement>("[aria-invalid=true]")
          ?.focus(),
      );
      return;
    }
    const controller = new AbortController();
    request.current = controller;
    setSaving(true);
    try {
      if (action === "create")
        setIssued(
          await createAccessKey(
            name.trim(),
            grants,
            controller.signal,
            canCreateRootWorkspaces,
          ),
        );
      else if (action === "rotate" && accessKey)
        setIssued(await rotateAccessKey(accessKey.id, controller.signal));
      else if (action === "revoke" && accessKey) {
        await revokeAccessKey(accessKey.id, controller.signal);
        toast.success(t("settingsKeyRevoked"));
        onClose();
      } else if (accessKey) {
        await updateAccessKeyGrants(
          accessKey.id,
          grants,
          controller.signal,
          canCreateRootWorkspaces,
          accessKey.accessVersion,
        );
        toast.success(t("settingsGrantsSaved"));
        onClose();
      }
      onChanged();
    } catch (error) {
      if (controller.signal.aborted) return;
      if (isAxiosError(error) && error.response?.status === 409) onChanged();
      setFailure(
        isAxiosError(error) && error.response?.status === 409
          ? "settingsConflict"
          : isAxiosError(error) && error.response?.status === 403
            ? "settingsForbidden"
            : "settingsKeySaveError",
      );
    } finally {
      if (!controller.signal.aborted) setSaving(false);
    }
  }
  return (
    <Dialog
      open
      onOpenChange={(open) => {
        if (!open && !pending && !issued) onClose();
      }}
    >
      <FormDialog
        variant="wide"
        className={
          editsGrants && !issued
            ? "settings-key-dialog settings-key-dialog-compact"
            : "settings-key-dialog"
        }
        title={
          <>
            <span className="settings-key-dialog-title-text">
              {t(issued ? "settingsKeyIssued" : titleKeys[action])}
            </span>
            {action === "grants" && !issued && accessKey && (
              <>
                <span
                  className="settings-key-title-name"
                  title={accessKey.name}
                >
                  {accessKey.name}
                </span>
                <Popover>
                  <PopoverTrigger asChild>
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon"
                      disabled={pending}
                      className="settings-key-title-info"
                      aria-label={t("settingsKeyIdentifier")}
                    >
                      <InfoIcon aria-hidden="true" />
                    </Button>
                  </PopoverTrigger>
                  <PopoverContent
                    className="settings-space-details"
                    align="start"
                    collisionPadding={16}
                    aria-label={t("settingsKeyIdentifier")}
                  >
                    <strong>{accessKey.name}</strong>
                    <code>{accessKey.prefix}.••••</code>
                  </PopoverContent>
                </Popover>
              </>
            )}
          </>
        }
        titleIcon={KeyRoundIcon}
        description={t(
          issued
            ? "settingsKeyOnce"
            : action === "rotate"
              ? "settingsRotateWhy"
              : action === "revoke"
                ? "settingsRevokeWhy"
                : action === "create"
                  ? "settingsCreateKeyWhy"
                  : "settingsGrantsExact",
        )}
        closeLabel={t("cancel")}
        pending={pending}
        error={failure ? t(failure) : undefined}
        preventDismiss={!!issued}
        showCloseButton={!issued}
        formRef={form}
        onSubmit={issued ? undefined : (event) => void submit(event)}
        bodyFooter={
          editsGrants && !issued ? (
            <Field
              orientation="horizontal"
              className="settings-key-creation"
              data-disabled={pending}
            >
              <Checkbox
                id="key-create-root"
                checked={canCreateRootWorkspaces}
                disabled={pending}
                onCheckedChange={(checked) => {
                  setCanCreateRootWorkspaces(checked === true);
                  if (checked === true)
                    setInvalid((previous) =>
                      previous.filter((field) => field !== "grants"),
                    );
                }}
                aria-describedby="key-create-root-description"
              />
              <FieldContent>
                <FieldLabel htmlFor="key-create-root">
                  {t("settingsCanCreateRootWorkspaces")}
                </FieldLabel>
                <FieldDescription id="key-create-root-description">
                  {t("settingsCreationPermissionCompact")}
                </FieldDescription>
              </FieldContent>
            </Field>
          ) : undefined
        }
        footer={
          issued ? (
            <Button ref={done} onClick={onClose}>
              <CheckIcon data-icon="inline-start" aria-hidden="true" />
              {t("settingsKeyDone")}
            </Button>
          ) : (
            <>
              <Button
                type="button"
                variant="outline"
                disabled={pending}
                onClick={onClose}
              >
                <XIcon data-icon="inline-start" aria-hidden="true" />
                {t("cancel")}
              </Button>
              <SubmitButton
                pending={pending}
                icon={ActionIcon}
                label={t(
                  action === "grants"
                    ? "settingsSaveAccess"
                    : titleKeys[action],
                )}
                pendingLabel={t("settingsSaving")}
                variant={action === "revoke" ? "destructive" : "default"}
                disabled={
                  inactive ||
                  failure === "settingsConflict" ||
                  (editsGrants &&
                    !workspaces.length &&
                    !canCreateRootWorkspaces)
                }
              />
            </>
          )
        }
      >
        {failure === "settingsConflict" && (
          <SettingsConflictRecovery
            {...recovery}
            onReload={() =>
              void recovery.reload((latest) => {
                const latestKey = latest.items.find(
                  (item) => item.id === accessKey?.id,
                );
                setAccessKey(latestKey);
                setWorkspaces(latest.workspaces);
                setGrants(latestKey?.workspaceIds ?? []);
                setCanCreateRootWorkspaces(
                  latestKey?.canCreateRootWorkspaces ?? false,
                );
                setName("");
                setInvalid([]);
                setFailure(
                  action !== "create" && (!latestKey || latestKey.revokedAt)
                    ? "settingsKeyInactive"
                    : undefined,
                );
                onChanged();
              })
            }
          />
        )}
        {issued ? (
          <div className="settings-issued">
            <p>
              <strong>{issued.key.name}</strong>
            </p>
            <div className="settings-secret">
              <span className="settings-secret-label">
                {t("settingsFullKey")}
              </span>
              <code>{issued.secret}</code>
              <CopySetting
                value={issued.secret}
                label={t("settingsCopyKey")}
                showLabel
              />
            </div>
            <p className="settings-inline-note">{t("settingsKeyUse")}</p>
          </div>
        ) : (
          <>
            {action === "create" && (
              <Field
                data-invalid={invalid.includes("name")}
                data-disabled={pending}
              >
                <FieldLabel htmlFor="key-name">
                  {t("settingsKeyName")}
                </FieldLabel>
                <Input
                  id="key-name"
                  value={name}
                  onChange={(event) => {
                    setName(event.target.value);
                    if (event.target.value.trim())
                      setInvalid((previous) =>
                        previous.filter((field) => field !== "name"),
                      );
                  }}
                  maxLength={200}
                  placeholder={t("settingsKeyNamePlaceholder")}
                  disabled={pending}
                  autoComplete="off"
                  aria-invalid={invalid.includes("name")}
                  aria-describedby={
                    invalid.includes("name") ? "key-name-error" : undefined
                  }
                />
                {invalid.includes("name") && (
                  <p id="key-name-error" className="settings-field-error">
                    {t("settingsKeyNameInvalid")}
                  </p>
                )}
              </Field>
            )}
            {accessKey && !editsGrants && (
              <p className="settings-selected-key">
                <strong>{accessKey.name}</strong>
                <code>{accessKey.prefix}.••••</code>
              </p>
            )}
            {editsGrants && (
              <FieldSet
                className="settings-workspaces gap-3"
                disabled={pending}
                data-invalid={invalid.includes("grants")}
                data-disabled={pending}
              >
                <FieldLegend variant="label" className="sr-only">
                  {t("settingsGrantedWorkspaces")}
                </FieldLegend>
                <FieldDescription
                  id="key-grants-description"
                  className="sr-only"
                >
                  {t("settingsGrantsExact")}
                </FieldDescription>
                {!workspaces.length && (
                  <p>
                    {t("settingsNoWorkspaces")}{" "}
                    <Link to="/spaces">{t("spaces")}</Link>
                  </p>
                )}
                {!!workspaces.length && (
                  <AccessKeyWorkspacePicker
                    workspaces={workspaces}
                    grants={grants}
                    disabled={pending}
                    invalid={invalid.includes("grants")}
                    onChange={(next) => {
                      setGrants(next);
                      if (next.length)
                        setInvalid((previous) =>
                          previous.filter((field) => field !== "grants"),
                        );
                    }}
                  />
                )}
                {invalid.includes("grants") && (
                  <p id="key-grants-error" className="settings-field-error">
                    {t("settingsGrantsInvalid")}
                  </p>
                )}
              </FieldSet>
            )}
          </>
        )}
      </FormDialog>
    </Dialog>
  );
}
