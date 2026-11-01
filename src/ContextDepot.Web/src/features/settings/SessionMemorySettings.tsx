import { useEffect, useRef, useState } from "react";
import { BrainIcon, CheckIcon, ChevronRightIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { RequestFeedback } from "@/components/feedback/RequestFeedback";
import { Spinner } from "@/components/ui/spinner";
import { Switch } from "@/components/ui/switch";
import { SettingsSection } from "@/pages/settings/SettingsSection";
import { requestFailure, type RequestFailure } from "@/lib/request-failure";
import {
  getAgentSessionSettings,
  saveAgentSessionSettings,
  type AgentSessionSettings,
} from "./settings-api";
import { SettingsResourceState } from "./SettingsResourceState";
import { useSettingsResource } from "./use-settings-resource";

export function SessionMemorySettings() {
  const { t } = useTranslation();
  const resource = useSettingsResource(getAgentSessionSettings);
  const [saved, setSaved] = useState<AgentSessionSettings>();
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<RequestFailure>();
  const request = useRef<AbortController | null>(null);
  useEffect(() => () => request.current?.abort(), []);

  const data =
    resource.pending || resource.error ? undefined : (saved ?? resource.data);
  const busy = saving || resource.pending;
  function reload() {
    setSaved(undefined);
    resource.refresh();
  }
  async function toggle(enabled: boolean) {
    if (request.current || !data || busy || resource.error) return;
    const controller = new AbortController();
    request.current = controller;
    setSaving(true);
    setSaveError(undefined);
    try {
      const result = await saveAgentSessionSettings(enabled, controller.signal);
      if (!controller.signal.aborted) setSaved(result);
    } catch (error) {
      if (!controller.signal.aborted) {
        setSaveError(requestFailure(error));
        reload();
      }
    } finally {
      if (!controller.signal.aborted) {
        request.current = null;
        setSaving(false);
      }
    }
  }
  return (
    <SettingsSection
      id="session-memory"
      title={t("settingsSessionMemoryTitle")}
      detail={t("settingsSessionMemoryWhy")}
      icon={BrainIcon}
      action={
        <span className="settings-save-status" role="status">
          {busy ? (
            <Spinner aria-hidden="true" />
          ) : data && !saveError ? (
            <CheckIcon aria-hidden="true" />
          ) : null}
          {t(
            saving
              ? "settingsSaving"
              : resource.pending
                ? "loading"
                : "settingsAutoSave",
          )}
        </span>
      }
    >
      <SettingsResourceState resource={resource} onRetry={reload} />
      {saveError && (
        <RequestFeedback
          title={t("settingsSessionMemorySaveError")}
          description={t("settingsSessionMemoryReconcile")}
          failure={saveError}
          onRetry={reload}
          pending={busy}
          compact
        />
      )}
      <div className="settings-session-memory-toggle" aria-busy={busy}>
        <div>
          <div className="settings-session-memory-label">
            <label htmlFor="session-memory-enabled">
              {t("settingsSessionMemoryEnable")}
            </label>
            <span
              id="session-memory-state"
              className="settings-session-memory-state"
              role="status"
            >
              {t(
                !data
                  ? "settingsSessionMemoryUnknown"
                  : data.enabled
                    ? "settingsSessionMemoryOn"
                    : "settingsSessionMemoryOff",
              )}
            </span>
          </div>
          <p
            id="session-memory-scope"
            className="settings-session-memory-scope"
          >
            {t("settingsSessionMemoryScope")}
          </p>
        </div>
        {data && (
          <Switch
            id="session-memory-enabled"
            checked={data.enabled}
            disabled={busy}
            aria-describedby="session-memory-state session-memory-scope"
            onCheckedChange={(enabled) => void toggle(enabled)}
          />
        )}
      </div>
      <div className="settings-session-memory-guidance">
        <section aria-labelledby="session-memory-disabled-title">
          <h3 id="session-memory-disabled-title">
            {t("settingsSessionMemoryDisabledTitle")}
          </h3>
          <p>{t("settingsSessionMemoryDisabledWhy")}</p>
        </section>
        <section aria-labelledby="session-memory-requirements-title">
          <h3 id="session-memory-requirements-title">
            {t("settingsSessionMemoryRequirementsTitle")}
          </h3>
          <p>{t("settingsSessionMemoryRequirements")}</p>
          <a
            className="settings-text-link settings-session-memory-link"
            href="#inference"
          >
            {t("settingsSessionMemoryInference")}
            <ChevronRightIcon aria-hidden="true" />
          </a>
        </section>
      </div>
    </SettingsSection>
  );
}
