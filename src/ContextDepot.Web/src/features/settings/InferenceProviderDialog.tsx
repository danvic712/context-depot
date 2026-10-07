import { useEffect, useRef, useState, type FormEvent } from "react";
import { isAxiosError } from "axios";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import {
  EyeIcon,
  EyeOffIcon,
  PlugIcon,
  ScanSearchIcon,
  MessageSquareIcon,
  SaveIcon,
} from "lucide-react";
import "@/styles/inference-dialog.css";
import type { Messages } from "@/lib/i18n";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Dialog } from "@/components/ui/dialog";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import {
  Field,
  FieldLabel,
  FieldDescription,
  FieldGroup,
} from "@/components/ui/field";
import { FormDialog } from "@/components/content/FormDialog";
import { SubmitButton } from "@/components/content/SubmitButton";
import {
  validateInferenceProviderDraft,
  type InferenceProvider,
  type InferenceProviderSettings,
  type InferenceRoute,
} from "./settings-api";
import { connectInferenceProviderDraft } from "./inference-connections";
import { inferenceProviderDraft } from "./inference-drafts";
import { SettingsConflictRecovery } from "./SettingsConflictRecovery";
import { useSettingsConflictRecovery } from "./use-settings-conflict-recovery";

import {
  savedInferenceEditor,
  type InferenceSettingsEditor,
} from "./inference-settings-editor";

export function InferenceProviderDialog({
  provider: initialProvider,
  capability,
  settings: initialSettings,
  editor = savedInferenceEditor,
  onClose,
  onSaved,
  onRefresh,
}: {
  provider?: InferenceProvider;
  capability?: InferenceRoute["capability"];
  settings: InferenceProviderSettings;
  editor?: InferenceSettingsEditor;
  onClose: () => void;
  onSaved: () => void;
  onRefresh: () => void;
}) {
  const { t } = useTranslation();
  const [snapshot, setSnapshot] = useState({
    provider: initialProvider,
    settings: initialSettings,
  });
  const { provider, settings } = snapshot;
  const embedding = settings.routes.find(
    (route) => route.capability === "embedding",
  )!;
  const chat = settings.routes.find((route) => route.capability === "chat")!;
  const [draft, setDraft] = useState(() =>
    inferenceProviderDraft(provider, settings, capability),
  );
  const [saving, setSaving] = useState(false);
  const [showApiKey, setShowApiKey] = useState(false);
  const recovery = useSettingsConflictRecovery(editor.load);
  const pending = saving || recovery.pending;
  const [invalid, setInvalid] = useState<string[]>([]);
  const [failure, setFailure] = useState<keyof Messages>();
  const request = useRef<AbortController | null>(null);
  const form = useRef<HTMLFormElement>(null);
  const preset = settings.presets.find((item) => item.kind === draft.kind)!;
  useEffect(() => () => request.current?.abort(), []);
  async function submit(event: FormEvent) {
    event.preventDefault();
    if (
      pending ||
      failure === "settingsConflict" ||
      failure === "settingsProviderRemoved"
    )
      return;
    const errors = validateInferenceProviderDraft(
      draft,
      provider?.hasApiKey ?? false,
    );
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
      const result = await editor.saveProvider(draft, controller.signal);
      if (controller.signal.aborted) return;
      const savedEmbedding = result.routes.find(
        (route) => route.capability === "embedding",
      );
      if (editor.isDraft) toast.success(t("setupDraftUpdated"));
      else if (
        draft.embedding.enabled &&
        savedEmbedding &&
        !savedEmbedding.isApplied
      )
        toast.warning(t("settingsInferenceSavedPending"));
      else toast.success(t("settingsProviderSaved"));
      onSaved();
      onClose();
    } catch (error) {
      if (controller.signal.aborted) return;
      if (isAxiosError(error) && error.response?.status === 409) onRefresh();
      setFailure(
        isAxiosError(error) && error.response?.status === 409
          ? "settingsConflict"
          : isAxiosError(error) && error.response?.status === 400
            ? "settingsInferenceInvalid"
            : isAxiosError(error) && error.response?.status === 403
              ? "settingsForbidden"
              : "settingsInferenceSaveError",
      );
    } finally {
      if (!controller.signal.aborted) setSaving(false);
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
      label: "settingsInferenceEndpoint",
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
  const capabilities: InferenceRoute["capability"][] = capability
    ? [capability]
    : ["embedding", "chat"];
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
        className="inference-config-dialog"
        titleIcon={
          capability === "embedding"
            ? ScanSearchIcon
            : capability === "chat"
              ? MessageSquareIcon
              : PlugIcon
        }
        variant="wide"
        title={t(
          provider
            ? provider.hasApiKey
              ? "settingsEditProvider"
              : "settingsConfigureProviderTitle"
            : capability
              ? "settingsConfigureCapability"
              : "settingsConnectProvider",
          {
            capability: t(
              capability === "embedding"
                ? "settingsEmbeddingTitle"
                : "settingsChatTitle",
            ),
          },
        )}
        description={t(
          capability ? "settingsConnectModelWhy" : "settingsProviderDialogWhy",
        )}
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
              icon={SaveIcon}
              pending={pending}
              disabled={
                failure === "settingsConflict" ||
                failure === "settingsProviderRemoved"
              }
              label={t(
                editor.isDraft ? "setupApplyDraft" : "settingsSaveProvider",
              )}
              pendingLabel={t("settingsSaving")}
            />
          </>
        }
      >
        {failure === "settingsConflict" && (
          <SettingsConflictRecovery
            {...recovery}
            onReload={() =>
              void recovery.reload((latest) => {
                const latestProvider = latest.providers.find(
                  (item) => item.id === provider?.id,
                );
                onRefresh();
                if (provider && !latestProvider) {
                  setFailure("settingsProviderRemoved");
                  return;
                }
                setSnapshot({ provider: latestProvider, settings: latest });
                setDraft(
                  inferenceProviderDraft(latestProvider, latest, capability),
                );
                setInvalid([]);
                setFailure(undefined);
              })
            }
          />
        )}
        <fieldset className="settings-provider-section" disabled={pending}>
          <legend>{t("settingsProviderConnection")}</legend>
          <FieldGroup className="settings-form-grid">
            <fieldset className="settings-provider-picker" disabled={pending}>
              <legend id="provider-kind-label">
                {t("settingsProviderType")}
              </legend>
              {provider ? (
                <p className="settings-provider-type-summary">
                  <strong>
                    {preset.kind === "custom"
                      ? t("settingsProviderCustom")
                      : preset.name}
                  </strong>
                  <span>
                    {t(
                      preset.supportsEmbedding
                        ? "settingsProviderBothCapabilities"
                        : "settingsProviderChatCapability",
                    )}
                  </span>
                </p>
              ) : (
                <Select
                  value={draft.kind}
                  disabled={pending}
                  onValueChange={(kind) => {
                    const option = settings.presets.find(
                      (item) => item.kind === kind,
                    )!;
                    setDraft((value) =>
                      connectInferenceProviderDraft(
                        value,
                        option,
                        settings,
                        capability,
                      ),
                    );
                    setInvalid([]);
                    setFailure(undefined);
                  }}
                >
                  <SelectTrigger
                    className="w-full"
                    aria-labelledby="provider-kind-label"
                  >
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {settings.presets
                      .filter(
                        (option) =>
                          capability !== "embedding" ||
                          option.supportsEmbedding,
                      )
                      .map((option) => (
                        <SelectItem key={option.kind} value={option.kind}>
                          {option.kind === "custom"
                            ? t("settingsProviderCustom")
                            : option.name}
                        </SelectItem>
                      ))}
                  </SelectContent>
                </Select>
              )}
            </fieldset>
            {common.map((field) => (
              <Field
                key={field.key}
                className={
                  field.key !== "name" ? "settings-form-full" : undefined
                }
                data-invalid={invalid.includes(field.key)}
                data-disabled={pending}
              >
                <FieldLabel htmlFor={`provider-${field.key}`}>
                  {field.label === "API Key" ? field.label : t(field.label)}
                </FieldLabel>
                <div
                  className={
                    field.key === "apiKey"
                      ? "inference-secret-input"
                      : undefined
                  }
                >
                  <Input
                    id={`provider-${field.key}`}
                    type={
                      field.key === "apiKey" && showApiKey ? "text" : field.type
                    }
                    value={draft[field.key]}
                    placeholder={
                      field.key === "endpoint"
                        ? preset.endpointPlaceholder
                        : undefined
                    }
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
                          : field.key === "endpoint"
                            ? "provider-endpoint-hint"
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
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon"
                      disabled={pending}
                      aria-label={t(
                        showApiKey
                          ? "settingsHideApiKey"
                          : "settingsShowApiKey",
                      )}
                      aria-pressed={showApiKey}
                      onClick={() => setShowApiKey((value) => !value)}
                    >
                      {showApiKey ? (
                        <EyeOffIcon aria-hidden="true" />
                      ) : (
                        <EyeIcon aria-hidden="true" />
                      )}
                    </Button>
                  )}
                </div>
                {field.key === "endpoint" && (
                  <FieldDescription id="provider-endpoint-hint">
                    {t(
                      draft.kind === "azure-openai"
                        ? "settingsProviderAzureEndpoint"
                        : "settingsProviderEndpointHint",
                    )}
                  </FieldDescription>
                )}
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
        </fieldset>
        <fieldset className="settings-provider-section" disabled={pending}>
          <legend>{t("settingsProviderModels")}</legend>
          {!capability && (
            <p className="settings-provider-section-hint">
              {t("settingsProviderModelsHint")}
            </p>
          )}
          <div className="settings-provider-model-editor">
            {capabilities.map((capability) => {
              const current = capability === "embedding" ? embedding : chat;
              const supported =
                capability === "chat" || preset.supportsEmbedding;
              return (
                <fieldset
                  key={capability}
                  className="settings-model-editor"
                  data-capability={capability}
                  data-disabled={!supported}
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
                      disabled={!supported}
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
                          !supported
                            ? "settingsProviderEmbeddingUnsupported"
                            : capability === "embedding"
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
                                  placeholder={
                                    field.key === "model"
                                      ? t(
                                          draft.kind === "azure-openai"
                                            ? "settingsProviderDeploymentPlaceholder"
                                            : "settingsProviderModelPlaceholder",
                                        )
                                      : undefined
                                  }
                                  maxLength={
                                    field.type === "text"
                                      ? field.max
                                      : undefined
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
                      {current.providerId &&
                        current.providerId !== draft.id && (
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
        </fieldset>
        {profileChanged && (
          <p className="settings-inline-note">{t("settingsProfileChange")}</p>
        )}
      </FormDialog>
    </Dialog>
  );
}
