import { useEffect, useRef } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";
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
  useEffect(() => {
    acknowledge.current?.focus();
  }, []);
  const url = new URL(mcpPath, window.location.origin).href;
  const configuration = mcpClientConfiguration(url, issued.secret);
  return (
    <section className="setup-issued" aria-labelledby="setup-key-issued-title">
      <h2 id="setup-key-issued-title">{t("setupKeyCreated")}</h2>
      <p>{t("settingsKeyOnce")}</p>
      <div className="settings-secret">
        <code>{issued.secret}</code>
        <CopySetting value={issued.secret} label={t("settingsCopyKey")} />
      </div>
      <section
        className="setup-client-example"
        aria-labelledby="setup-client-saved-title"
      >
        <div>
          <h3 id="setup-client-saved-title">{t("setupMcpExample")}</h3>
          <CopySetting value={configuration} label={t("setupMcpExample")} />
        </div>
        <pre>
          <code>{configuration}</code>
        </pre>
      </section>
      <Button ref={acknowledge} onClick={onAcknowledge}>
        {t("setupKeyAcknowledge")}
      </Button>
    </section>
  );
}
