import { useEffect, useRef, useState, type FormEvent } from "react";
import type { Messages } from "@/lib/i18n";
import { isAxiosError } from "axios";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Dialog } from "@/components/ui/dialog";
import {
  Field,
  FieldGroup,
  FieldLabel,
  FieldDescription,
} from "@/components/ui/field";
import { FormDialog } from "@/components/content/FormDialog";
import { SubmitButton } from "@/components/content/SubmitButton";
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
      <FormDialog
        variant="wide"
        title={t(
          route.capability === "embedding"
            ? "settingsEmbeddingTitle"
            : "settingsChatTitle",
        )}
        description={t("settingsAiDialogWhy")}
        closeLabel={t("cancel")}
        pending={pending}
        error={failure ? t(failure) : undefined}
        preventDismiss
        formRef={form}
        onSubmit={(event) => void submit(event)}
        footer={
          <>
            <Button
              variant="outline"
              type="button"
              disabled={pending}
              onClick={onClose}
            >
              {t("cancel")}
            </Button>
            <SubmitButton
              pending={pending}
              label={t("settingsSaveAi")}
              pendingLabel={t("settingsSaving")}
            />
          </>
        }
      >
        <p className="settings-inline-note">
          {t("settingsProtocol")}: <code>openai-compatible</code>
        </p>
        <FieldGroup className="settings-form-grid">
          {fields
            .filter(
              (field) =>
                field.key !== "dimensions" || route.capability === "embedding",
            )
            .map((field) => (
              <Field
                data-invalid={invalid.includes(field.key)}
                data-disabled={pending}
                key={field.key}
              >
                <FieldLabel htmlFor={`ai-${field.key}`}>
                  {field.label === "API Key" ? field.label : t(field.label)}
                </FieldLabel>
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
                  <FieldDescription id="ai-key-hint">
                    {t(
                      route.hasApiKey
                        ? "settingsApiKeyKeep"
                        : "settingsApiKeyRequired",
                    )}
                  </FieldDescription>
                )}
                {invalid.includes(field.key) && (
                  <p
                    id={`ai-${field.key}-error`}
                    className="settings-field-error"
                  >
                    {t(`settingsInvalid_${field.key}`)}
                  </p>
                )}
              </Field>
            ))}
        </FieldGroup>
        {profileChanged && (
          <p className="settings-inline-note">{t("settingsProfileChange")}</p>
        )}
      </FormDialog>
    </Dialog>
  );
}
