import { useEffect, useRef, useState, type FormEvent } from "react";
import type { Messages } from "@/lib/i18n";
import { isAxiosError } from "axios";
import { LoaderCircleIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogTitle,
  DialogFooter,
} from "@/components/ui/dialog";
import {
  saveAiRoute,
  validateAiDraft,
  type AiDraft,
  type AiRoute,
} from "./settings-api";

export function AiRouteDialog({
  route,
  onClose,
  onSaved,
}: {
  route: AiRoute;
  onClose: () => void;
  onSaved: () => void;
}) {
  const { t } = useTranslation();
  const [draft, setDraft] = useState<AiDraft>({
    providerName: route.providerName ?? "",
    endpoint: route.endpoint ?? "",
    model: route.model ?? "",
    dimensions: route.dimensions,
    timeoutSeconds: route.timeoutSeconds,
    apiKey: "",
    updatedAt: route.updatedAt,
  });
  const [pending, setPending] = useState(false);
  const [invalid, setInvalid] = useState<string[]>([]);
  const [failure, setFailure] = useState<keyof Messages>();
  const request = useRef<AbortController | null>(null);
  const form = useRef<HTMLFormElement>(null);
  useEffect(() => () => request.current?.abort(), []);
  const fields: {
    key: Exclude<keyof AiDraft, "updatedAt">;
    label: keyof Messages | "API Key";
    type: "text" | "url" | "number" | "password";
    max: number;
  }[] = [
    { key: "providerName", label: "settingsProvider", type: "text", max: 200 },
    { key: "endpoint", label: "settingsAiEndpoint", type: "url", max: 2000 },
    { key: "model", label: "settingsModel", type: "text", max: 300 },
    {
      key: "dimensions",
      label: "settingsDimensions",
      type: "number",
      max: 16000,
    },
    {
      key: "timeoutSeconds",
      label: "settingsTimeout",
      type: "number",
      max: 300,
    },
    { key: "apiKey", label: "API Key", type: "password", max: 8192 },
  ];
  async function submit(event: FormEvent) {
    event.preventDefault();
    if (pending) return;
    const errors = validateAiDraft(draft, route.capability, route.hasApiKey);
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
      const result = await saveAiRoute(
        route.capability,
        draft,
        controller.signal,
      );
      if (controller.signal.aborted) return;
      if (result.capability === "embedding" && !result.isApplied)
        toast.warning(t("settingsAiSavedPending"));
      else
        toast.success(
          t(
            result.capability === "embedding"
              ? "settingsAiActivated"
              : "settingsChatSaved",
          ),
        );
      onSaved();
      onClose();
    } catch (error) {
      if (controller.signal.aborted) return;
      setFailure(
        isAxiosError(error) && error.response?.status === 409
          ? "settingsConflict"
          : isAxiosError(error) && error.response?.status === 400
            ? "settingsAiInvalid"
            : isAxiosError(error) && error.response?.status === 403
              ? "settingsForbidden"
              : "settingsAiSaveError",
      );
    } finally {
      if (!controller.signal.aborted) setPending(false);
    }
  }
  const profileChanged =
    route.capability === "embedding" &&
    !!route.model &&
    (draft.providerName !== route.providerName ||
      draft.endpoint !== route.endpoint ||
      draft.model !== route.model ||
      draft.dimensions !== route.dimensions);
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
          event.preventDefault();
        }}
      >
        <DialogTitle className="text-xl font-semibold">
          {t(
            route.capability === "embedding"
              ? "settingsEmbeddingTitle"
              : "settingsChatTitle",
          )}
        </DialogTitle>
        <DialogDescription className="mt-2 text-sm text-muted-foreground">
          {t("settingsAiDialogWhy")}
        </DialogDescription>
        <form
          ref={form}
          className="settings-form"
          onSubmit={(event) => void submit(event)}
          noValidate
          aria-busy={pending}
        >
          <p className="settings-inline-note">
            {t("settingsProtocol")}: <code>openai-compatible</code>
          </p>
          <div className="settings-form-grid">
            {fields
              .filter(
                (field) =>
                  field.key !== "dimensions" ||
                  route.capability === "embedding",
              )
              .map((field) => (
                <div className="settings-field" key={field.key}>
                  <label htmlFor={`ai-${field.key}`}>
                    {field.label === "API Key" ? field.label : t(field.label)}
                  </label>
                  <Input
                    id={`ai-${field.key}`}
                    type={field.type}
                    disabled={pending}
                    value={draft[field.key] ?? ""}
                    maxLength={field.type !== "number" ? field.max : undefined}
                    min={field.type === "number" ? 1 : undefined}
                    max={field.type === "number" ? field.max : undefined}
                    step={field.type === "number" ? 1 : undefined}
                    autoComplete={
                      field.type === "password" ? "new-password" : "off"
                    }
                    spellCheck={false}
                    autoCapitalize="none"
                    aria-invalid={invalid.includes(field.key)}
                    aria-describedby={
                      invalid.includes(field.key)
                        ? `ai-${field.key}-error`
                        : field.key === "apiKey"
                          ? "ai-key-hint"
                          : undefined
                    }
                    onChange={(event) =>
                      setDraft((previous) => ({
                        ...previous,
                        [field.key]:
                          field.type === "number"
                            ? event.target.value === ""
                              ? null
                              : Number(event.target.value)
                            : event.target.value,
                      }))
                    }
                  />
                  {field.key === "apiKey" && (
                    <p id="ai-key-hint" className="settings-field-hint">
                      {t(
                        route.hasApiKey
                          ? "settingsApiKeyKeep"
                          : "settingsApiKeyRequired",
                      )}
                    </p>
                  )}
                  {invalid.includes(field.key) && (
                    <p
                      id={`ai-${field.key}-error`}
                      className="settings-field-error"
                    >
                      {t(`settingsInvalid_${field.key}`)}
                    </p>
                  )}
                </div>
              ))}
          </div>
          {profileChanged && (
            <p className="settings-inline-note">{t("settingsProfileChange")}</p>
          )}
          {failure && (
            <p className="settings-field-error" role="alert">
              {t(failure)}
            </p>
          )}
          <DialogFooter>
            <Button
              variant="outline"
              type="button"
              disabled={pending}
              onClick={onClose}
            >
              {t("cancel")}
            </Button>
            <Button type="submit" disabled={pending}>
              {pending && (
                <LoaderCircleIcon className="animate-spin" aria-hidden="true" />
              )}
              {t(pending ? "settingsSaving" : "settingsSaveAi")}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
