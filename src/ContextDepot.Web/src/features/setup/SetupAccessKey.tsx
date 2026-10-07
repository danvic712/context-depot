import { useEffect, useRef } from "react";
import { useTranslation } from "react-i18next";
import { Input } from "@/components/ui/input";
import { Field, FieldLabel } from "@/components/ui/field";
import { CopySetting } from "@/features/settings/SettingsStatus";
import type { SetupWorkspace } from "./setup-api";
import { mcpClientConfiguration } from "./mcp-configuration";

export function SetupAccessKey({
  workspace,
  mcpPath,
  enabled,
  name,
  invalid,
  onEnabled,
  onName,
}: {
  workspace: SetupWorkspace;
  mcpPath: "/mcp";
  enabled: boolean;
  name: string;
  invalid: boolean;
  onEnabled: (enabled: boolean) => void;
  onName: (name: string) => void;
}) {
  const { t } = useTranslation();
  const input = useRef<HTMLInputElement>(null);
  useEffect(() => {
    if (invalid) input.current?.focus();
  }, [invalid]);
  const url =
    typeof window === "undefined"
      ? mcpPath
      : new URL(mcpPath, window.location.origin).href;
  const configuration = mcpClientConfiguration(url);
  return (
    <div className="setup-mcp">
      <div className="setup-mcp-controls">
        <dl className="setup-connection-details">
          <div>
            <dt>{t("setupMcpAddress")}</dt>
            <dd>
              <code>{url}</code>
              <CopySetting value={url} label={t("setupMcpAddress")} />
            </dd>
          </div>
          <div>
            <dt>{t("setupMcpGrant")}</dt>
            <dd>
              <strong>{workspace.name}</strong>
              <code>{workspace.path}</code>
            </dd>
          </div>
        </dl>
        <label className="settings-model-toggle" htmlFor="setup-mcp-enabled">
          <input
            type="checkbox"
            id="setup-mcp-enabled"
            checked={enabled}
            onChange={(event) => onEnabled(event.target.checked)}
          />
          <strong>{t("setupKeyOnFinish")}</strong>
        </label>
        {enabled && (
          <Field data-invalid={invalid}>
            <FieldLabel htmlFor="setup-key-name">
              {t("settingsKeyName")}
            </FieldLabel>
            <Input
              id="setup-key-name"
              ref={input}
              value={name}
              onChange={(event) => onName(event.target.value)}
              maxLength={200}
              autoComplete="off"
              placeholder={t("setupKeyNamePlaceholder")}
              aria-invalid={invalid}
              aria-describedby={invalid ? "setup-key-name-error" : undefined}
            />
            {invalid && (
              <p
                id="setup-key-name-error"
                className="settings-field-error"
                role="alert"
              >
                {t("settingsKeyNameInvalid")}
              </p>
            )}
          </Field>
        )}
        <p className="setup-note">{t("setupKeyDraftWhy")}</p>
      </div>
      <section
        className="setup-client-example"
        aria-labelledby="setup-client-example-title"
      >
        <div>
          <h2 id="setup-client-example-title">{t("setupMcpExample")}</h2>
          <CopySetting value={configuration} label={t("setupMcpExample")} />
        </div>
        <p>{t("setupMcpExampleWhy")}</p>
        <pre>
          <code>{configuration}</code>
        </pre>
      </section>
    </div>
  );
}
