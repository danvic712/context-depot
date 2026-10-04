import { useEffect, useRef, useState, type FormEvent } from "react";
import type { Messages } from "@/lib/i18n";
import { isAxiosError } from "axios";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { Link } from "react-router";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Dialog } from "@/components/ui/dialog";
import {
  Field,
  FieldLabel,
  FieldSet,
  FieldLegend,
  FieldDescription,
} from "@/components/ui/field";
import { FormDialog } from "@/components/content/FormDialog";
import { SubmitButton } from "@/components/content/SubmitButton";
import { CopySetting } from "./SettingsStatus";
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
  async function submit(event: FormEvent) {
    event.preventDefault();
    if (pending || inactive || failure === "settingsConflict") return;
    const errors = [];
    if (action === "create" && (!name.trim() || name.trim().length > 200))
      errors.push("name");
    if (editsGrants && !grants.length) errors.push("grants");
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
          await createAccessKey(name.trim(), grants, controller.signal),
        );
      else if (action === "rotate" && accessKey)
        setIssued(await rotateAccessKey(accessKey.id, controller.signal));
      else if (action === "revoke" && accessKey) {
        await revokeAccessKey(accessKey.id, controller.signal);
        toast.success(t("settingsKeyRevoked"));
        onClose();
      } else if (accessKey) {
        await updateAccessKeyGrants(accessKey.id, grants, controller.signal);
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
        title={t(issued ? "settingsKeyIssued" : titleKeys[action])}
        description={t(
          issued
            ? "settingsKeyOnce"
            : action === "rotate"
              ? "settingsRotateWhy"
              : action === "revoke"
                ? "settingsRevokeWhy"
                : "settingsGrantsWhy",
        )}
        closeLabel={t("cancel")}
        pending={pending}
        error={failure ? t(failure) : undefined}
        preventDismiss={!!issued}
        showCloseButton={!issued}
        formRef={form}
        onSubmit={issued ? undefined : (event) => void submit(event)}
        footer={
          issued ? (
            <Button ref={done} onClick={onClose}>
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
                {t("cancel")}
              </Button>
              <SubmitButton
                pending={pending}
                label={t(titleKeys[action])}
                pendingLabel={t("settingsSaving")}
                variant={action === "revoke" ? "destructive" : "default"}
                disabled={
                  inactive ||
                  failure === "settingsConflict" ||
                  (editsGrants && !workspaces.length)
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
              <code>{issued.secret}</code>
              <CopySetting value={issued.secret} label={t("settingsCopyKey")} />
            </div>
            <p className="settings-inline-note">
              {t("settingsHeaderName")} <code>X-ContextDepot-Key</code>
            </p>
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
                  onChange={(event) => setName(event.target.value)}
                  maxLength={200}
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
            {accessKey && (
              <p className="settings-selected-key">
                <strong>{accessKey.name}</strong>
                <code>{accessKey.prefix}.••••••••</code>
              </p>
            )}
            {editsGrants && (
              <FieldSet
                className="settings-workspaces"
                disabled={pending}
                data-invalid={invalid.includes("grants")}
                data-disabled={pending}
              >
                <FieldLegend variant="label">
                  {t("settingsGrantedWorkspaces")}
                </FieldLegend>
                <FieldDescription>{t("settingsGrantsExact")}</FieldDescription>
                {!workspaces.length && (
                  <p>
                    {t("settingsNoWorkspaces")}{" "}
                    <Link to="/spaces">{t("spaces")}</Link>
                  </p>
                )}
                <div className="settings-workspace-options">
                  {workspaces.map((workspace, index) => (
                    <label key={workspace.id}>
                      <input
                        type="checkbox"
                        checked={grants.includes(workspace.id)}
                        aria-invalid={index === 0 && invalid.includes("grants")}
                        aria-describedby={
                          invalid.includes("grants")
                            ? "key-grants-error"
                            : undefined
                        }
                        onChange={(event) =>
                          setGrants((previous) =>
                            event.target.checked
                              ? [...previous, workspace.id]
                              : previous.filter((id) => id !== workspace.id),
                          )
                        }
                      />
                      <span>
                        <strong>{workspace.name}</strong>
                        <code>{workspace.path}</code>
                      </span>
                    </label>
                  ))}
                </div>
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
