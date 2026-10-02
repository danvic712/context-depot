import { useEffect, useRef, useState, type FormEvent } from "react";
import type { Messages } from "@/lib/i18n";
import { isAxiosError } from "axios";
import { LoaderCircleIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { Link } from "react-router";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogTitle,
  DialogFooter,
} from "@/components/ui/dialog";
import { CopySetting } from "./SettingsStatus";
import {
  createAccessKey,
  rotateAccessKey,
  revokeAccessKey,
  updateAccessKeyGrants,
  type AccessKey,
  type KeyWorkspace,
  type IssuedKey,
} from "./settings-api";

export type KeyAction = "create" | "grants" | "rotate" | "revoke";
const titleKeys = {
  create: "settingsCreateKey",
  grants: "settingsEditGrants",
  rotate: "settingsRotate",
  revoke: "settingsRevoke",
} as const;
export function AccessKeyDialog({
  action,
  accessKey,
  workspaces,
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
  const [name, setName] = useState("");
  const [grants, setGrants] = useState(accessKey?.workspaceIds ?? []);
  const [issued, setIssued] = useState<IssuedKey>();
  const [pending, setPending] = useState(false);
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
    if (pending) return;
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
    setPending(true);
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
      setFailure(
        isAxiosError(error) && error.response?.status === 409
          ? "settingsConflict"
          : isAxiosError(error) && error.response?.status === 403
            ? "settingsForbidden"
            : "settingsKeySaveError",
      );
    } finally {
      if (!controller.signal.aborted) setPending(false);
    }
  }
  return (
    <Dialog
      open
      onOpenChange={(open) => {
        if (!open && !pending) onClose();
      }}
    >
      <DialogContent
        className="settings-dialog"
        closeLabel={t("cancel")}
        closeDisabled={pending}
        onEscapeKeyDown={(event) => {
          if (pending) event.preventDefault();
        }}
        onPointerDownOutside={(event) => {
          if (pending || issued) event.preventDefault();
        }}
      >
        <DialogTitle className="text-xl font-semibold">
          {t(issued ? "settingsKeyIssued" : titleKeys[action])}
        </DialogTitle>
        <DialogDescription className="mt-2 text-sm text-muted-foreground">
          {issued
            ? t("settingsKeyOnce")
            : action === "rotate"
              ? t("settingsRotateWhy")
              : action === "revoke"
                ? t("settingsRevokeWhy")
                : t("settingsGrantsWhy")}
        </DialogDescription>
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
            <Button ref={done} className="w-full" onClick={onClose}>
              {t("settingsKeyDone")}
            </Button>
          </div>
        ) : (
          <form
            ref={form}
            className="settings-form"
            onSubmit={(event) => void submit(event)}
            noValidate
            aria-busy={pending}
          >
            {action === "create" && (
              <div className="settings-field">
                <label htmlFor="key-name">{t("settingsKeyName")}</label>
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
              </div>
            )}
            {accessKey && (
              <p className="settings-selected-key">
                <strong>{accessKey.name}</strong>
                <code>{accessKey.prefix}.••••••••</code>
              </p>
            )}
            {editsGrants && (
              <fieldset className="settings-workspaces" disabled={pending}>
                <legend>{t("settingsGrantedWorkspaces")}</legend>
                <p>{t("settingsGrantsExact")}</p>
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
              </fieldset>
            )}
            {failure && (
              <p className="settings-field-error" role="alert">
                {t(failure)}
              </p>
            )}
            <DialogFooter>
              <Button
                type="button"
                variant="outline"
                disabled={pending}
                onClick={onClose}
              >
                {t("cancel")}
              </Button>
              <Button
                type="submit"
                variant={action === "revoke" ? "destructive" : "default"}
                disabled={pending || (editsGrants && !workspaces.length)}
              >
                {pending && (
                  <LoaderCircleIcon
                    className="animate-spin"
                    aria-hidden="true"
                  />
                )}
                {t(pending ? "settingsSaving" : titleKeys[action])}
              </Button>
            </DialogFooter>
          </form>
        )}
      </DialogContent>
    </Dialog>
  );
}
