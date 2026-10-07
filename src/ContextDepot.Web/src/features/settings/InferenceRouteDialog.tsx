import { useEffect, useRef, useState, type FormEvent } from "react";
import { isAxiosError } from "axios";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
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
  FieldDescription,
  FieldGroup,
  FieldLabel,
} from "@/components/ui/field";
import { FormDialog } from "@/components/content/FormDialog";
import { SubmitButton } from "@/components/content/SubmitButton";
import {
  PlusIcon,
  ScanSearchIcon,
  MessageSquareIcon,
  SaveIcon,
} from "lucide-react";
import "@/styles/inference-dialog.css";
import { availableInferenceProviders } from "./inference-connections";
import {
  validateInferenceRouteDraft,
  type InferenceProviderSettings,
  type InferenceRoute,
} from "./settings-api";

import { inferenceRouteDraft } from "./inference-drafts";
import { SettingsConflictRecovery } from "./SettingsConflictRecovery";
import { useSettingsConflictRecovery } from "./use-settings-conflict-recovery";

import {
  savedInferenceEditor,
  type InferenceSettingsEditor,
} from "./inference-settings-editor";

export function InferenceRouteDialog({
  route: initialRoute,
  settings: initialSettings,
  editor = savedInferenceEditor,
  onClose,
  onConnect,
  onSaved,
  onRefresh,
}: {
  route: InferenceRoute;
  settings: InferenceProviderSettings;
  editor?: InferenceSettingsEditor;
  onClose: () => void;
  onConnect: () => void;
  onSaved: () => void;
  onRefresh: () => void;
}) {
  const { t } = useTranslation();
  const [snapshot, setSnapshot] = useState({
    route: initialRoute,
    settings: initialSettings,
  });
  const { route, settings } = snapshot;
  const [draft, setDraft] = useState(() =>
    inferenceRouteDraft(route, settings),
  );
  const [saving, setSaving] = useState(false);
  const recovery = useSettingsConflictRecovery(editor.load);
  const pending = saving || recovery.pending;
  const [invalid, setInvalid] = useState<string[]>([]);
  const [failure, setFailure] = useState<keyof Messages>();
  const form = useRef<HTMLFormElement>(null);
  const request = useRef<AbortController | null>(null);
  useEffect(() => () => request.current?.abort(), []);
  const providers = availableInferenceProviders(settings, route.capability);
  const currentProvider = settings.providers.find(
    (provider) => provider.id === route.providerId,
  );
  const choices =
    currentProvider && !providers.includes(currentProvider)
      ? [currentProvider, ...providers]
      : providers;
  const selected = providers.find(
    (provider) => provider.id === draft.providerId,
  );
  async function submit(event: FormEvent) {
    event.preventDefault();
    if (pending || failure === "settingsConflict") return;
    const errors = validateInferenceRouteDraft(
      draft,
      route.capability,
      settings,
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
      const saved = await editor.saveRoute(
        route.capability,
        draft,
        controller.signal,
      );
      if (controller.signal.aborted) return;
      if (editor.isDraft) toast.success(t("setupDraftUpdated"));
      else if (saved.runtimeState === "pending")
        toast.warning(t("settingsInferenceSavedPending"));
      else
        toast.success(
          t("settingsRouteSaved", {
            capability: t(
              route.capability === "embedding"
                ? "settingsEmbeddingTitle"
                : "settingsChatTitle",
            ),
          }),
        );
      onSaved();
      onClose();
    } catch (error) {
      if (controller.signal.aborted) return;
      if (isAxiosError(error) && error.response?.status === 409) onRefresh();
      setFailure(
        isAxiosError(error) && error.response?.status === 409
          ? "settingsConflict"
          : isAxiosError(error) && error.response?.status === 403
            ? "settingsForbidden"
            : isAxiosError(error) && error.response?.status === 400
              ? "settingsInferenceInvalid"
              : "settingsRouteSaveError",
      );
    } finally {
      if (!controller.signal.aborted) setSaving(false);
    }
  }
  const fields = [
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
          route.capability === "embedding" ? ScanSearchIcon : MessageSquareIcon
        }
        title={t("settingsConfigureCapability", {
          capability: t(
            route.capability === "embedding"
              ? "settingsEmbeddingTitle"
              : "settingsChatTitle",
          ),
        })}
        description={t("settingsRouteDialogWhy")}
        closeLabel={t("cancel")}
        pending={pending}
        preventDismiss
        formRef={form}
        error={failure ? t(failure) : undefined}
        onSubmit={(event) => void submit(event)}
        footer={
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
              icon={SaveIcon}
              pending={pending}
              disabled={failure === "settingsConflict"}
              label={t(
                editor.isDraft ? "setupApplyDraft" : "settingsSaveRoute",
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
                const latestRoute = latest.routes.find(
                  (item) => item.capability === route.capability,
                )!;
                setSnapshot({ route: latestRoute, settings: latest });
                setDraft(inferenceRouteDraft(latestRoute, latest));
                setInvalid([]);
                setFailure(undefined);
                onRefresh();
              })
            }
          />
        )}
        <Field data-invalid={invalid.includes("providerId")}>
          <FieldLabel htmlFor="route-provider">
            {t("settingsProviderType")}
          </FieldLabel>
          <Select
            value={draft.providerId ?? "unconfigured"}
            disabled={pending}
            onValueChange={(value) => {
              const provider = providers.find((item) => item.id === value);
              setDraft((current) => ({
                ...current,
                providerId: provider?.id ?? null,
                providerUpdatedAt: provider?.updatedAt ?? null,
                model: "",
                dimensions: null,
              }));
              setInvalid([]);
            }}
          >
            <SelectTrigger
              id="route-provider"
              className="w-full"
              aria-invalid={invalid.includes("providerId")}
              aria-describedby="route-provider-hint"
            >
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="unconfigured">
                {t("settingsState_unconfigured")}
              </SelectItem>
              {choices.map((provider) => (
                <SelectItem
                  key={provider.id}
                  value={provider.id}
                  disabled={!provider.hasApiKey || !provider.endpoint}
                >
                  {provider.name}
                  {!provider.hasApiKey || !provider.endpoint
                    ? ` · ${t("settingsState_unconfigured")}`
                    : ""}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <Button
            type="button"
            variant="outline"
            size="sm"
            className="settings-connect-another"
            disabled={pending}
            onClick={onConnect}
          >
            <PlusIcon aria-hidden="true" />
            {t("settingsConnectAnotherProvider")}
          </Button>
          <FieldDescription id="route-provider-hint">
            {selected?.endpoint ?? t("settingsRouteProviderHint")}
          </FieldDescription>
          {invalid.includes("providerId") && (
            <p className="settings-field-error">
              {t("settingsRouteProviderHint")}
            </p>
          )}
        </Field>
        {draft.providerId ? (
          <FieldGroup className="settings-form-grid">
            {fields
              .filter(
                (field) =>
                  field.key !== "dimensions" ||
                  route.capability === "embedding",
              )
              .map((field) => (
                <Field
                  key={field.key}
                  className={
                    field.key === "model" ? "settings-form-full" : undefined
                  }
                  data-invalid={invalid.includes(field.key)}
                >
                  <FieldLabel htmlFor={`route-${field.key}`}>
                    {t(field.label)}
                  </FieldLabel>
                  <Input
                    id={`route-${field.key}`}
                    type={field.type}
                    disabled={pending}
                    value={draft[field.key] ?? ""}
                    placeholder={
                      field.key === "model"
                        ? t(
                            selected?.kind === "azure-openai"
                              ? "settingsProviderDeploymentPlaceholder"
                              : "settingsProviderModelPlaceholder",
                          )
                        : undefined
                    }
                    maxLength={field.type === "text" ? field.max : undefined}
                    min={field.type === "number" ? 1 : undefined}
                    max={field.type === "number" ? field.max : undefined}
                    step={field.type === "number" ? 1 : undefined}
                    autoComplete="off"
                    spellCheck={false}
                    aria-invalid={invalid.includes(field.key)}
                    aria-describedby={
                      invalid.includes(field.key)
                        ? `route-${field.key}-error`
                        : undefined
                    }
                    onChange={(event) => {
                      setInvalid((current) =>
                        current.filter((key) => key !== field.key),
                      );
                      setDraft((current) => ({
                        ...current,
                        [field.key]:
                          field.type === "text"
                            ? event.target.value
                            : field.key === "dimensions" && !event.target.value
                              ? null
                              : Number(event.target.value),
                      }));
                    }}
                  />
                  {invalid.includes(field.key) && (
                    <p
                      id={`route-${field.key}-error`}
                      className="settings-field-error"
                    >
                      {t(`settingsInvalid_${field.key}`)}
                    </p>
                  )}
                </Field>
              ))}
          </FieldGroup>
        ) : route.providerId ? (
          <p className="settings-inline-note">{t("settingsRouteDisable")}</p>
        ) : null}
        {draft.providerId &&
          route.capability === "embedding" &&
          route.model &&
          (draft.providerId !== route.providerId ||
            draft.model !== route.model ||
            draft.dimensions !== route.dimensions) && (
            <p className="settings-inline-note">{t("settingsProfileChange")}</p>
          )}
      </FormDialog>
    </Dialog>
  );
}
