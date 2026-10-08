import { useEffect, useRef } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import type { IssuedKey } from "@/features/settings/settings-api";
import { CopySetting } from "@/features/settings/SettingsStatus";
import { mcpClientConfiguration } from "./mcp-configuration";

export function SetupKeyReceipt({
  issued,
  mcpPath,
  onAcknowledge,
}: {
  issued: IssuedKey;
  mcpPath: "/mcp";
  onAcknowledge: () => void;
}) {
  const { t } = useTranslation();
  const acknowledge = useRef<HTMLButtonElement>(null);
  const secretField = useRef<HTMLTextAreaElement>(null);
  const configurationField = useRef<HTMLTextAreaElement>(null);
  function selectField(field: HTMLTextAreaElement | null) {
    field?.focus({ preventScroll: true });
    field?.select();
  }
  useEffect(() => {
    acknowledge.current?.focus();
  }, []);
  const url = new URL(mcpPath, window.location.origin).href;
  const configuration = mcpClientConfiguration(url, issued.secret);
  return (
    <section className="setup-issued" aria-labelledby="setup-key-issued-title">
      <header className="setup-issued-heading">
        <h2 id="setup-key-issued-title">{t("setupKeyCreated")}</h2>
        <p>{t("setupKeyOnce")}</p>
      </header>
      <div className="setup-issued-secret">
        <label htmlFor="setup-issued-key">{t("setupReviewKeys")}</label>
        <Textarea
          id="setup-issued-key"
          ref={secretField}
          readOnly
          value={issued.secret}
          rows={1}
          spellCheck={false}
          autoComplete="off"
          aria-describedby="setup-copy-manual-hint"
          onFocus={(event) => event.currentTarget.select()}
          onClick={(event) => event.currentTarget.select()}
        />
        <CopySetting
          value={issued.secret}
          label={t("settingsCopyKey")}
          showLabel
          onCopyError={() => selectField(secretField.current)}
        />
      </div>
      <section
        className="setup-client-example"
        aria-labelledby="setup-client-saved-title"
      >
        <div>
          <h3 id="setup-client-saved-title">{t("setupMcpExample")}</h3>
          <CopySetting
            value={configuration}
            label={t("setupCopyConfiguration")}
            showLabel
            onCopyError={() => selectField(configurationField.current)}
          />
        </div>
        <Textarea
          ref={configurationField}
          readOnly
          value={configuration}
          rows={11}
          spellCheck={false}
          autoComplete="off"
          aria-labelledby="setup-client-saved-title"
          aria-describedby="setup-copy-manual-hint"
          onFocus={(event) => event.currentTarget.select()}
          onClick={(event) => event.currentTarget.select()}
        />
      </section>
      <p id="setup-copy-manual-hint">{t("setupCopyManualHint")}</p>
      <Button ref={acknowledge} onClick={onAcknowledge}>
        {t("setupKeyAcknowledge")}
      </Button>
    </section>
  );
}
