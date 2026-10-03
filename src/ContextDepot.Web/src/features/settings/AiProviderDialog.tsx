import { useEffect, useRef, useState, type FormEvent } from "react";
import { isAxiosError } from "axios";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import type { Messages } from "@/lib/i18n";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Dialog } from "@/components/ui/dialog";
import {
  Field,
  FieldLabel,
  FieldDescription,
  FieldGroup,
} from "@/components/ui/field";
import { FormDialog } from "@/components/content/FormDialog";
import { SubmitButton } from "@/components/content/SubmitButton";
import {
  saveAiProvider,
  validateAiProviderDraft,
  type AiProvider,
  type AiProviderDraft,
  type AiProviderSettings,
  type AiModelDraft,
} from "./settings-api";

export function AiProviderDialog({
  provider,
  settings,
  onClose,
  onSaved,
}: {
  provider?: AiProvider;
  settings: AiProviderSettings;
  onClose: () => void;
  onSaved: () => void;
}) {
  const { t } = useTranslation();
  const embedding = settings.routes.find(
    (route) => route.capability === "embedding",
  )!;
  const chat = settings.routes.find((route) => route.capability === "chat")!;
  function modelDraft(capability: "embedding" | "chat"): AiModelDraft {
    const route = capability === "embedding" ? embedding : chat;
    const selected = !!provider && route.providerId === provider.id;
    return {
      enabled: selected,
      model: selected ? (route.model ?? "") : "",
      dimensions:
        capability === "embedding"
          ? selected
            ? route.dimensions
            : null
          : null,
      timeoutSeconds: selected ? route.timeoutSeconds : 30,
    };
  }
  const [draft, setDraft] = useState<AiProviderDraft>({
    id: provider?.id ?? null,
    name: provider?.name ?? "",
    endpoint: provider?.endpoint ?? "",
    apiKey: "",
    updatedAt: provider?.updatedAt ?? null,
    embedding: modelDraft("embedding"),
    chat: modelDraft("chat"),
    embeddingUpdatedAt: embedding.updatedAt,
    chatUpdatedAt: chat.updatedAt,
  });
  const [pending, setPending] = useState(false);
  const [invalid, setInvalid] = useState<string[]>([]);
  const [failure, setFailure] = useState<keyof Messages>();
  const request = useRef<AbortController | null>(null);
  const form = useRef<HTMLFormElement>(null);
  useEffect(() => () => request.current?.abort(), []);
  async function submit(event: FormEvent) {
    event.preventDefault();
    if (pending) return;
    const errors = validateAiProviderDraft(draft, provider?.hasApiKey ?? false);
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
      const result = await saveAiProvider(draft, controller.signal);
      if (controller.signal.aborted) return;
      const savedEmbedding = result.routes.find(
        (route) => route.capability === "embedding",
      );
      if (
        draft.embedding.enabled &&
        savedEmbedding &&
        !savedEmbedding.isApplied
      )
        toast.warning(t("settingsAiSavedPending"));
      else toast.success(t("settingsProviderSaved"));
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
  const common = [
    {
      key: "name",
      label: "settingsProvider",
      type: "text",
      max: 200,
      error: "settingsInvalid_providerName",
    },
    {
      key: "endpoint",
      label: "settingsAiEndpoint",
      type: "url",
      max: 2000,
      error: "settingsInvalid_endpoint",
    },
    {
      key: "apiKey",
      label: "API Key",
      type: "password",
      max: 8192,
      error: "settingsInvalid_apiKey",
    },
  ] as const;
  const modelFields = [
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
  ] as const;
  const profileChanged =
    draft.embedding.enabled &&
    !!embedding.model &&
    (embedding.providerId !== draft.id ||
      draft.name !== embedding.providerName ||
      draft.endpoint !== embedding.endpoint ||
      draft.embedding.model !== embedding.model ||
      draft.embedding.dimensions !== embedding.dimensions);
  return (
    <Dialog
      open
      onOpenChange={(open) => {
        if (!open && !pending) onClose();
      }}
    >
      <FormDialog
        variant="wide"
        title={t(provider ? "settingsEditProvider" : "settingsAddProvider")}
        description={t("settingsProviderDialogWhy")}
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
              label={t("settingsSaveProvider")}
              pendingLabel={t("settingsSaving")}
            />
          </>
        }
      >
        <FieldGroup className="settings-form-grid">
          {common.map((field) => (
            <Field
              key={field.key}
              className={
                field.key === "apiKey" ? "settings-form-full" : undefined
              }
              data-invalid={invalid.includes(field.key)}
              data-disabled={pending}
            >
              <FieldLabel htmlFor={`provider-${field.key}`}>
                {field.label === "API Key" ? field.label : t(field.label)}
              </FieldLabel>
              <Input
                id={`provider-${field.key}`}
                type={field.type}
                value={draft[field.key]}
                disabled={pending}
                maxLength={field.max}
                autoComplete={
                  field.type === "password" ? "new-password" : "off"
                }
                spellCheck={false}
                autoCapitalize="none"
                aria-invalid={invalid.includes(field.key)}
                aria-describedby={
                  invalid.includes(field.key)
                    ? `provider-${field.key}-error`
                    : field.key === "apiKey"
                      ? "provider-key-hint"
                      : undefined
                }
                onChange={(event) => {
                  setInvalid((value) =>
                    value.filter((key) => key !== field.key),
                  );
                  setDraft((value) => ({
                    ...value,
                    [field.key]: event.target.value,
                  }));
                }}
              />
              {field.key === "apiKey" && (
                <FieldDescription id="provider-key-hint">
                  {t(
                    provider?.hasApiKey
                      ? "settingsApiKeyKeep"
                      : "settingsApiKeyRequired",
                  )}
                </FieldDescription>
              )}
              {invalid.includes(field.key) && (
                <p
                  id={`provider-${field.key}-error`}
                  className="settings-field-error"
                >
                  {t(field.error)}
                </p>
              )}
            </Field>
          ))}
        </FieldGroup>
        <div className="settings-provider-model-editor">
          {(["embedding", "chat"] as const).map((capability) => {
            const current = capability === "embedding" ? embedding : chat;
            return (
              <fieldset
                key={capability}
                className="settings-model-editor"
                data-capability={capability}
                disabled={pending}
              >
                <legend className="sr-only">
                  {t(
                    capability === "embedding"
                      ? "settingsEmbeddingTitle"
                      : "settingsChatTitle",
                  )}
                </legend>
                <label className="settings-model-toggle">
                  <input
                    type="checkbox"
                    checked={draft[capability].enabled}
                    onChange={(event) => {
                      if (!event.target.checked)
                        setInvalid((value) =>
                          value.filter((key) => !key.startsWith(capability)),
                        );
                      setDraft((value) => ({
                        ...value,
                        [capability]: {
                          ...value[capability],
                          enabled: event.target.checked,
                        },
                      }));
                    }}
                  />
                  <span>
                    <strong>
                      {t(
                        capability === "embedding"
                          ? "settingsEmbeddingTitle"
                          : "settingsChatTitle",
                      )}
                    </strong>
                    <span>
                      {t(
                        capability === "embedding"
                          ? "settingsEmbeddingWhy"
                          : "settingsChatWhy",
                      )}
                    </span>
                  </span>
                </label>
                {draft[capability].enabled && (
                  <>
                    <FieldGroup className="settings-model-fields">
                      {modelFields
                        .filter(
                          (field) =>
                            field.key !== "dimensions" ||
                            capability === "embedding",
                        )
                        .map((field) => {
                          const id = `provider-${capability}-${field.key}`,
                            error = `${capability}.${field.key}`;
                          return (
                            <Field
                              key={field.key}
                              data-invalid={invalid.includes(error)}
                            >
                              <FieldLabel htmlFor={id}>
                                {t(field.label)}
                              </FieldLabel>
                              <Input
                                id={id}
                                type={field.type}
                                value={draft[capability][field.key] ?? ""}
                                maxLength={
                                  field.type === "text" ? field.max : undefined
                                }
                                min={field.type === "number" ? 1 : undefined}
                                max={
                                  field.type === "number"
                                    ? field.max
                                    : undefined
                                }
                                step={field.type === "number" ? 1 : undefined}
                                autoComplete="off"
                                spellCheck={false}
                                aria-invalid={invalid.includes(error)}
                                aria-describedby={
                                  invalid.includes(error)
                                    ? `${id}-error`
                                    : undefined
                                }
                                onChange={(event) => {
                                  setInvalid((value) =>
                                    value.filter((key) => key !== error),
                                  );
                                  setDraft((value) => ({
                                    ...value,
                                    [capability]: {
                                      ...value[capability],
                                      [field.key]:
                                        field.type === "text"
                                          ? event.target.value
                                          : field.key === "dimensions" &&
                                              !event.target.value
                                            ? null
                                            : Number(event.target.value),
                                    },
                                  }));
                                }}
                              />
                              {invalid.includes(error) && (
                                <p
                                  id={`${id}-error`}
                                  className="settings-field-error"
                                >
                                  {t(`settingsInvalid_${field.key}`)}
                                </p>
                              )}
                            </Field>
                          );
                        })}
                    </FieldGroup>
                    {current.providerId && current.providerId !== draft.id && (
                      <p className="settings-provider-note">
                        {t("settingsProviderSwitch", {
                          name: current.providerName,
                        })}
                      </p>
                    )}
                  </>
                )}
                {!draft[capability].enabled &&
                  current.providerId === draft.id &&
                  provider && (
                    <p className="settings-provider-note">
                      {t("settingsProviderDisable")}
                    </p>
                  )}
              </fieldset>
            );
          })}
        </div>
        {profileChanged && (
          <p className="settings-inline-note">{t("settingsProfileChange")}</p>
        )}
      </FormDialog>
    </Dialog>
  );
}
