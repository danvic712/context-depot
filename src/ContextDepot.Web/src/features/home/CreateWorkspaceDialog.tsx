import {
  useCallback,
  useEffect,
  useRef,
  useState,
  type FormEvent,
  type ReactNode,
} from "react";
import axios from "axios";
import { FolderPlusIcon, PlusIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Dialog, DialogTrigger } from "@/components/ui/dialog";
import { Field, FieldLabel, FieldDescription } from "@/components/ui/field";
import { FormDialog } from "@/components/content/FormDialog";
import { SubmitButton } from "@/components/content/SubmitButton";
import { createWorkspace } from "@/features/spaces/workspace-management-api";
import { suggestWorkspacePath } from "@/features/spaces/workspace-path";
import { useSpaceDialogMotion } from "@/features/spaces/use-space-dialog-motion";
import "@/styles/create-workspace.css";

type FieldErrors = Partial<Record<"name" | "path", string>>;
type CreateFailure =
  "spaceCreateError" | "spaceCreateForbidden" | "spaceChildParentUnavailable";
export function CreateWorkspaceDialog({
  onCreated,
  parent,
  children,
  triggerClassName,
}: {
  onCreated: () => void | Promise<void>;
  parent?: { id: string; name: string; path: string };
  children?: ReactNode;
  triggerClassName?: string;
}) {
  const { t } = useTranslation();
  const label = t(parent ? "newChildSpace" : "newSpace");
  const [open, setOpen] = useState(false);
  const [name, setName] = useState("");
  const [path, setPath] = useState("");
  const [pathCustomized, setPathCustomized] = useState(false);
  const [description, setDescription] = useState("");
  const [errors, setErrors] = useState<FieldErrors>({});
  const [failure, setFailure] = useState<CreateFailure>();
  const [pending, setPending] = useState(false);
  const request = useRef<AbortController | null>(null);
  const nameInput = useRef<HTMLInputElement>(null);
  const pathInput = useRef<HTMLInputElement>(null);
  const triggerContent = children ?? (
    <>
      <PlusIcon data-icon="inline-start" aria-hidden="true" />
      <span>{label}</span>
    </>
  );
  const created = useRef(false);
  const handleClosed = useCallback(() => {
    setOpen(false);
    if (created.current) {
      created.current = false;
      setName("");
      setPath("");
      setPathCustomized(false);
      setDescription("");
    }
  }, []);
  const {
    ref: dialogRef,
    overlayRef,
    close: closeDialog,
    open: reopenDialog,
  } = useSpaceDialogMotion(handleClosed);
  useEffect(() => () => request.current?.abort(), []);
  async function submit(event: FormEvent) {
    event.preventDefault();
    if (pending) return;
    const next: FieldErrors = {};
    if (!name.trim() || name.trim().length > 200)
      next.name = t("spaceNameInvalid");
    if (
      path.trim().length > 100 ||
      !/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(path.trim())
    )
      next.path = t("spacePathInvalid");
    setErrors(next);
    setFailure(undefined);
    if (next.name || next.path) {
      (next.name ? nameInput : pathInput).current?.focus();
      return;
    }
    setPending(true);
    const controller = new AbortController();
    request.current = controller;
    try {
      await createWorkspace(
        {
          name: name.trim(),
          path: path.trim(),
          description: description.trim(),
        },
        controller.signal,
        parent?.id,
      );
      created.current = true;
      closeDialog();
      toast.success(t(parent ? "spaceChildCreated" : "spaceCreated"));
      void onCreated();
    } catch (error) {
      if (controller.signal.aborted) return;
      const response = axios.isAxiosError(error) ? error.response : undefined;
      if (response?.status === 409) {
        setErrors({
          path: t(parent ? "spaceChildPathConflict" : "spacePathConflict"),
        });
        requestAnimationFrame(() => pathInput.current?.focus());
      } else if (response?.status === 401 || response?.status === 403) {
        setFailure("spaceCreateForbidden");
      } else if (response?.status === 404 && parent) {
        setFailure("spaceChildParentUnavailable");
      } else if (response?.status === 400) {
        const fields: unknown = response.data?.errors;
        if (fields && typeof fields === "object") {
          const invalid: FieldErrors = {};
          if ("name" in fields || "Name" in fields)
            invalid.name = t("spaceNameInvalid");
          if ("path" in fields || "Path" in fields)
            invalid.path = t("spacePathInvalid");
          setErrors(invalid);
          if (invalid.name || invalid.path)
            requestAnimationFrame(() =>
              (invalid.name ? nameInput : pathInput).current?.focus(),
            );
          else setFailure("spaceCreateError");
        } else setFailure("spaceCreateError");
      } else setFailure("spaceCreateError");
    } finally {
      if (!controller.signal.aborted) setPending(false);
    }
  }
  return (
    <Dialog
      open={open}
      onOpenChange={(value) => {
        if (pending) return;
        if (value) {
          setOpen(true);
          reopenDialog();
        } else closeDialog();
      }}
    >
      <DialogTrigger asChild>
        {triggerClassName ? (
          <button type="button" className={triggerClassName}>
            {triggerContent}
          </button>
        ) : (
          <Button type="button" className="home-create-action">
            {triggerContent}
          </Button>
        )}
      </DialogTrigger>
      <FormDialog
        ref={dialogRef}
        data-space-motion="spring"
        overlayProps={{ ref: overlayRef }}
        className="create-space-dialog"
        overlayClassName="space-dialog-overlay"
        titleIcon={FolderPlusIcon}
        title={label}
        description={
          parent
            ? t("createChildSpaceDescription", { name: parent.name })
            : t("createSpaceDescription")
        }
        closeLabel={t("cancel")}
        pending={pending}
        error={failure ? t(failure) : undefined}
        onSubmit={(event) => void submit(event)}
        footer={
          <>
            <Button
              variant="outline"
              type="button"
              disabled={pending}
              onClick={() => closeDialog()}
            >
              {t("cancel")}
            </Button>
            <SubmitButton
              icon={PlusIcon}
              pending={pending}
              label={label}
              pendingLabel={t("spaceCreating")}
            />
          </>
        }
      >
        <Field data-invalid={!!errors.name} data-disabled={pending}>
          <FieldLabel htmlFor="space-name">{t("spaceNameLabel")}</FieldLabel>
          <Input
            id="space-name"
            ref={nameInput}
            placeholder={t("setupSpaceNamePlaceholder")}
            value={name}
            onChange={(event) => {
              const nextName = event.target.value;
              setName(nextName);
              if (!pathCustomized) setPath(suggestWorkspacePath(nextName));
              setErrors((current) => ({
                ...current,
                name: undefined,
                ...(!pathCustomized && { path: undefined }),
              }));
            }}
            maxLength={200}
            required
            disabled={pending}
            aria-invalid={!!errors.name}
            aria-describedby={`space-name-hint${errors.name ? " space-name-error" : ""}`}
            autoComplete="off"
          />
          <FieldDescription id="space-name-hint">
            {t("setupSpaceNameHint")}
          </FieldDescription>
          {errors.name && (
            <p id="space-name-error" className="form-dialog-error" role="alert">
              {errors.name}
            </p>
          )}
        </Field>
        <Field data-invalid={!!errors.path} data-disabled={pending}>
          <FieldLabel htmlFor="space-path">{t("spacesAddress")}</FieldLabel>
          <Input
            id="space-path"
            ref={pathInput}
            value={path}
            onChange={(event) => {
              setPath(event.target.value);
              setPathCustomized(true);
              setErrors((current) => ({ ...current, path: undefined }));
            }}
            placeholder={t("setupSpacePathPlaceholder")}
            maxLength={100}
            required
            disabled={pending}
            aria-invalid={!!errors.path}
            aria-describedby={`space-path-hint${parent ? " space-path-preview" : ""}${errors.path ? " space-path-error" : ""}`}
            autoComplete="off"
            autoCapitalize="none"
            spellCheck={false}
          />
          <FieldDescription id="space-path-hint">
            {t("spaceCreatePathHint")}
          </FieldDescription>
          {parent && (
            <FieldDescription
              id="space-path-preview"
              className="space-path-preview"
            >
              {t("spacesChildPath", {
                path: `${parent.path}/${path.trim() || "…"}`,
              })}
            </FieldDescription>
          )}
          {errors.path && (
            <p id="space-path-error" className="form-dialog-error" role="alert">
              {errors.path}
            </p>
          )}
        </Field>
        <Field data-disabled={pending}>
          <FieldLabel htmlFor="space-description">
            {t("spaceDescriptionLabel")}
          </FieldLabel>
          <Textarea
            id="space-description"
            placeholder={t("setupSpaceDescriptionPlaceholder")}
            value={description}
            onChange={(event) => setDescription(event.target.value)}
            disabled={pending}
            rows={3}
            aria-describedby="space-description-hint"
          />
          <FieldDescription id="space-description-hint">
            {t("spaceCreateDescriptionHint")}
          </FieldDescription>
        </Field>
      </FormDialog>
    </Dialog>
  );
}
