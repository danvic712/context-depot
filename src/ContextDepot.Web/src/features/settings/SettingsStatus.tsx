import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { CopyIcon } from "lucide-react";
import { Button } from "@/components/ui/button";

const states = {
  unknown: "settingsState_unknown",
  available: "settingsState_available",
  unavailable: "settingsState_unavailable",
  active: "settingsState_active",
  revoked: "settingsState_revoked",
  configured: "settingsState_configured",
  unconfigured: "settingsState_unconfigured",
  degraded: "settingsState_degraded",
  complete: "settingsState_complete",
  repairing: "settingsState_repairing",
  pending: "settingsState_pending",
} as const;
export function SettingsStatus({ state }: { state: string }) {
  const { t } = useTranslation();
  return (
    <span className="settings-status" data-state={state}>
      {t(states[state as keyof typeof states] ?? states.unknown)}
    </span>
  );
}
export function CopySetting({
  value,
  label,
}: {
  value: string;
  label: string;
}) {
  const { t } = useTranslation();
  return (
    <Button
      variant="outline"
      size="icon"
      type="button"
      aria-label={label}
      title={label}
      onClick={() => {
        void (async () => {
          try {
            await navigator.clipboard.writeText(value);
            toast.success(t("settingsCopied"));
          } catch {
            toast.error(t("settingsCopyError"));
          }
        })();
      }}
    >
      <CopyIcon aria-hidden="true" />
    </Button>
  );
}
