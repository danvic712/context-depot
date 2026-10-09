import {
  useEffect,
  useRef,
  useState,
  type FormEvent,
  type ReactNode,
} from "react";
import axios from "axios";
import { PlusIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { Button, buttonVariants } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Dialog, DialogTrigger } from "@/components/ui/dialog";
import { Field, FieldLabel, FieldDescription } from "@/components/ui/field";
import { FormDialog } from "@/components/content/FormDialog";
import { SubmitButton } from "@/components/content/SubmitButton";
import { createWorkspace } from "./home-api";
import { suggestWorkspacePath } from "@/features/spaces/workspace-path";
import "@/styles/create-workspace.css";

type FieldErrors = Partial<Record<"name" | "path", string>>;
export function CreateWorkspaceDialog({
  onCreated,
  children,
  triggerClassName,
}: {
  onCreated: () => void | Promise<void>;
  children?: ReactNode;
  triggerClassName?: string;
}) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const [name, setName] = useState("");
  const [path, setPath] = useState("");
  const [pathCustomized, setPathCustomized] = useState(false);
  const [description, setDescription] = useState("");
  const [errors, setErrors] = useState<FieldErrors>({});
  const [failure, setFailure] = useState(false);
  const [pending, setPending] = useState(false);
  const request = useRef<AbortController | null>(null);
  const trigger = useRef<HTMLButtonElement>(null);
  const nameInput = useRef<HTMLInputElement>(null);
  const pathInput = useRef<HTMLInputElement>(null);
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
    setFailure(false);
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
      );
      setOpen(false);
      setName("");
      setPath("");
      setPathCustomized(false);
      setDescription("");
      toast.success(t("spaceCreated"));
      void onCreated();
    } catch (error) {
      if (controller.signal.aborted) return;
      if (axios.isAxiosError(error) && error.response?.status === 409) {
        setErrors({ path: t("spacePathConflict") });
        requestAnimationFrame(() => pathInput.current?.focus());
      } else if (axios.isAxiosError(error) && error.response?.status === 400) {
        const fields: unknown = error.response.data?.errors;
        if (fields && typeof fields === "object") {
          const invalid: FieldErrors = {};
          if ("name" in fields || "Name" in fields)
            invalid.name = t("spaceNameInvalid");
          if ("path" in fields || "Path" in fields)
            invalid.path = t("spacePathInvalid");
          setErrors(invalid);
          setFailure(!invalid.name && !invalid.path);
          requestAnimationFrame(() =>
            (invalid.name ? nameInput : pathInput).current?.focus(),
          );
        } else setFailure(true);
      } else setFailure(true);
    } finally {
      if (!controller.signal.aborted) setPending(false);
    }
  }
  return (
    <Dialog
      open={open}
      onOpenChange={(value) => {
        if (!pending) setOpen(value);
      }}
    >
      <DialogTrigger asChild>
        <button
          type="button"
          ref={trigger}
          className={
            triggerClassName ?? `${buttonVariants()} home-create-action`
          }
        >
          {children ?? (
            <>
              <PlusIcon aria-hidden="true" />
              <span>{t("newSpace")}</span>
            </>
          )}
        </button>
      </DialogTrigger>
      <FormDialog
        title={t("newSpace")}
        description={t("createSpaceDescription")}
        closeLabel={t("cancel")}
        pending={pending}
        error={failure ? t("spaceCreateError") : undefined}
        onSubmit={(event) => void submit(event)}
        onCloseAutoFocus={(event) => {
          event.preventDefault();
          requestAnimationFrame(() => trigger.current?.focus());
        }}
        footer={
          <>
            <Button
              variant="outline"
              type="button"
              disabled={pending}
              onClick={() => setOpen(false)}
            >
              {t("cancel")}
            </Button>
            <SubmitButton
              pending={pending}
              label={t("newSpace")}
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
          <FieldLabel htmlFor="space-path">
            {t("setupSpacePathLabel")}
          </FieldLabel>
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
            aria-describedby={
              errors.path
                ? "space-path-error space-path-hint"
                : "space-path-hint"
            }
            autoComplete="off"
            autoCapitalize="none"
            spellCheck={false}
          />
          <FieldDescription id="space-path-hint">
            {t("setupSpacePathHint")}
          </FieldDescription>
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
          />
        </Field>
      </FormDialog>
    </Dialog>
  );
}
