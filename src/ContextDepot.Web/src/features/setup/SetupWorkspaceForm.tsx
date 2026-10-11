import { useRef, useState, type FormEvent } from "react";
import { useTranslation } from "react-i18next";
import { ArrowRightIcon } from "lucide-react";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Field, FieldLabel, FieldDescription } from "@/components/ui/field";
import { SubmitButton } from "@/components/content/SubmitButton";
import type { CreateWorkspace } from "@/features/home/home-api";
import type { SetupWorkspace } from "./setup-api";
import { suggestWorkspacePath } from "@/features/spaces/workspace-path";

export function SetupWorkspaceForm({
  workspace,
  pathCustomized,
  pending,
  onStart,
  onDraftChange,
  onPathCustomized,
}: {
  workspace: SetupWorkspace | null;
  pathCustomized: boolean;
  pending: boolean;
  onStart: (draft: CreateWorkspace) => void;
  onDraftChange: (draft: CreateWorkspace) => void;
  onPathCustomized: () => void;
}) {
  const { t } = useTranslation();
  const [name, setName] = useState(workspace?.name ?? "");
  const [path, setPath] = useState(workspace?.path ?? "");
  const [description, setDescription] = useState(workspace?.description ?? "");
  const [invalid, setInvalid] = useState<string[]>([]);
  const form = useRef<HTMLFormElement>(null);
  function submit(event: FormEvent) {
    event.preventDefault();
    if (pending) return;
    const fields: string[] = [];
    if (!name.trim() || name.trim().length > 200) fields.push("name");
    if (
      path.trim().length > 100 ||
      !/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(path.trim())
    )
      fields.push("path");
    if (description.trim().length > 2000) fields.push("description");
    setInvalid(fields);
    if (fields.length) {
      requestAnimationFrame(() =>
        form.current
          ?.querySelector<HTMLElement>("[aria-invalid=true]")
          ?.focus(),
      );
      return;
    }
    onStart({
      name: name.trim(),
      path: path.trim(),
      description: description.trim(),
    });
  }
  return (
    <form
      ref={form}
      className="setup-workspace-form"
      onSubmit={submit}
      noValidate
    >
      <Field data-invalid={invalid.includes("name")}>
        <FieldLabel htmlFor="setup-space-name">
          {t("spaceNameLabel")}
        </FieldLabel>
        <Input
          id="setup-space-name"
          placeholder={t("setupSpaceNamePlaceholder")}
          value={name}
          onChange={(e) => {
            const nextName = e.target.value;
            const nextPath = pathCustomized
              ? path
              : suggestWorkspacePath(nextName);
            setName(nextName);
            setPath(nextPath);
            setInvalid((fields) =>
              fields.filter(
                (field) =>
                  field !== "name" && (pathCustomized || field !== "path"),
              ),
            );
            onDraftChange({ name: nextName, path: nextPath, description });
          }}
          required
          maxLength={200}
          autoComplete="off"
          disabled={pending}
          aria-invalid={invalid.includes("name")}
          aria-describedby={`setup-name-hint${invalid.includes("name") ? " setup-name-error" : ""}`}
        />
        <FieldDescription id="setup-name-hint">
          {t("setupSpaceNameHint")}
        </FieldDescription>
        {invalid.includes("name") && (
          <p
            id="setup-name-error"
            className="settings-field-error"
            role="alert"
          >
            {t("spaceNameInvalid")}
          </p>
        )}
      </Field>
      <Field data-invalid={invalid.includes("path")}>
        <FieldLabel htmlFor="setup-space-path">
          {t("setupSpacePathLabel")}
        </FieldLabel>
        <Input
          id="setup-space-path"
          placeholder={t("setupSpacePathPlaceholder")}
          value={path}
          onChange={(e) => {
            setPath(e.target.value);
            onPathCustomized();
            setInvalid((fields) => fields.filter((field) => field !== "path"));
            onDraftChange({ name, path: e.target.value, description });
          }}
          required
          maxLength={100}
          autoComplete="off"
          autoCapitalize="none"
          spellCheck={false}
          disabled={pending}
          aria-invalid={invalid.includes("path")}
          aria-describedby={`setup-path-hint${invalid.includes("path") ? " setup-path-error" : ""}`}
        />
        <FieldDescription id="setup-path-hint">
          {t("setupSpacePathHint")}
        </FieldDescription>
        {invalid.includes("path") && (
          <p
            id="setup-path-error"
            className="settings-field-error"
            role="alert"
          >
            {t("setupSpacePathInvalid")}
          </p>
        )}
      </Field>
      <Field
        className="setup-workspace-description"
        data-invalid={invalid.includes("description")}
      >
        <FieldLabel htmlFor="setup-space-description">
          {t("spaceDescriptionLabel")}
        </FieldLabel>
        <Textarea
          id="setup-space-description"
          placeholder={t("setupSpaceDescriptionPlaceholder")}
          value={description}
          onChange={(e) => {
            setDescription(e.target.value);
            onDraftChange({ name, path, description: e.target.value });
          }}
          rows={3}
          maxLength={2000}
          disabled={pending}
          aria-invalid={invalid.includes("description")}
          aria-describedby={
            invalid.includes("description")
              ? "setup-description-error"
              : undefined
          }
        />
        {invalid.includes("description") && (
          <p
            id="setup-description-error"
            className="settings-field-error"
            role="alert"
          >
            {t("setupDescriptionInvalid")}
          </p>
        )}
      </Field>
      <div className="setup-actions setup-workspace-actions">
        <p>{t("setupWorkspaceNext")}</p>
        <SubmitButton
          icon={ArrowRightIcon}
          pending={pending}
          label={t("setupContinue")}
          pendingLabel={t(workspace ? "settingsSaving" : "spaceCreating")}
        />
      </div>
    </form>
  );
}
