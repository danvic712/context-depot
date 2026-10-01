import { useEffect, useRef, useState, type FormEvent } from "react";
import axios from "axios";
import { LoaderCircleIcon, PlusIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { Button, buttonVariants } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogTitle,
  DialogTrigger,
} from "@/components/ui/dialog";
import { createWorkspace } from "./home-api";

type FieldErrors = Partial<Record<"name" | "path", string>>;
export function CreateWorkspaceDialog({
  onCreated,
  preview = false,
  compact = false,
}: {
  onCreated: () => void | Promise<void>;
  preview?: boolean;
  compact?: boolean;
}) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  const [name, setName] = useState("");
  const [path, setPath] = useState("");
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
            compact
              ? `${buttonVariants()} home-create-action`
              : "home-space-tile home-create-tile"
          }
          onClick={(event) => {
            if (preview) {
              event.preventDefault();
              toast.info(t("createPreviewHint"));
            }
          }}
        >
          {compact ? (
            <>
              <PlusIcon aria-hidden="true" />
              <span>{t("newSpace")}</span>
            </>
          ) : (
            <>
              <span className="home-space-icon">
                <PlusIcon aria-hidden="true" />
              </span>
              <strong>{t("newSpace")}</strong>
              <span className="home-space-description">
                {t("createSpaceDescription")}
              </span>
              <span className="home-space-path">{t("createSpaceExample")}</span>
              <span className="home-space-meta">{t("createSpaceFooter")}</span>
            </>
          )}
        </button>
      </DialogTrigger>
      <DialogContent
        closeLabel={t("cancel")}
        closeDisabled={pending}
        onCloseAutoFocus={(event) => {
          event.preventDefault();
          requestAnimationFrame(() => trigger.current?.focus());
        }}
        onEscapeKeyDown={(event) => {
          if (pending) event.preventDefault();
        }}
        onPointerDownOutside={(event) => {
          if (pending) event.preventDefault();
        }}
      >
        <DialogTitle className="text-xl font-semibold">
          {t("newSpace")}
        </DialogTitle>
        <DialogDescription className="mt-2 text-sm text-muted-foreground">
          {t("createSpaceDescription")}
        </DialogDescription>
        <form
          className="home-create-form"
          onSubmit={(event) => void submit(event)}
          noValidate
          aria-busy={pending}
        >
          <label htmlFor="space-name">{t("spaceNameLabel")}</label>
          <Input
            id="space-name"
            ref={nameInput}
            value={name}
            onChange={(event) => setName(event.target.value)}
            maxLength={200}
            required
            disabled={pending}
            aria-invalid={!!errors.name}
            aria-describedby={errors.name ? "space-name-error" : undefined}
            autoComplete="off"
          />
          {errors.name && (
            <p id="space-name-error" className="home-field-error" role="alert">
              {errors.name}
            </p>
          )}
          <label htmlFor="space-path">{t("spacePathLabel")}</label>
          <Input
            id="space-path"
            ref={pathInput}
            value={path}
            onChange={(event) => setPath(event.target.value)}
            placeholder="research-notes"
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
          <p id="space-path-hint" className="home-field-hint">
            {t("spacePathHint")}
          </p>
          {errors.path && (
            <p id="space-path-error" className="home-field-error" role="alert">
              {errors.path}
            </p>
          )}
          <label htmlFor="space-description">
            {t("spaceDescriptionLabel")}
          </label>
          <Textarea
            id="space-description"
            value={description}
            onChange={(event) => setDescription(event.target.value)}
            disabled={pending}
            rows={3}
          />
          {failure && (
            <p className="home-field-error" role="alert">
              {t("spaceCreateError")}
            </p>
          )}
          <div className="home-dialog-actions">
            <Button
              variant="outline"
              type="button"
              disabled={pending}
              onClick={() => setOpen(false)}
            >
              {t("cancel")}
            </Button>
            <Button type="submit" disabled={pending}>
              {pending && (
                <LoaderCircleIcon className="animate-spin" aria-hidden="true" />
              )}
              {pending ? t("spaceCreating") : t("newSpace")}
            </Button>
          </div>
        </form>
      </DialogContent>
    </Dialog>
  );
}
