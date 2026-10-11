import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { CopyIcon } from "lucide-react";
import {
  StatusBadge,
  type StatusTone,
} from "@/components/feedback/StatusBadge";
import { Button } from "@/components/ui/button";
import copyToClipboard from "copy-to-clipboard";

const states = {
  draft: "setupDraftState",
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
const tones: Record<string, StatusTone> = {
  draft: "neutral",
  available: "success",
  active: "success",
  complete: "success",
  configured: "neutral",
  unavailable: "danger",
  revoked: "neutral",
  unconfigured: "warning",
  degraded: "warning",
  pending: "warning",
  repairing: "warning",
  unknown: "neutral",
};
export function SettingsStatus({ state }: { state: string }) {
  const { t } = useTranslation();
  return (
    <StatusBadge tone={tones[state] ?? "neutral"}>
      {t(states[state as keyof typeof states] ?? states.unknown)}
    </StatusBadge>
  );
}
export function CopySetting({
  value,
  label,
  showLabel = false,
  onCopyError,
}: {
  value: string;
  label: string;
  showLabel?: boolean;
  onCopyError?: () => void;
}) {
  const { t } = useTranslation();
  return (
    <Button
      variant="outline"
      size={showLabel ? "default" : "icon"}
      type="button"
      aria-label={label}
      title={label}
      onClick={() => {
        void (async () => {
          try {
            if (!(await copyToClipboard(value)))
              throw new Error("Clipboard unavailable");
            toast.success(t("settingsCopied"));
          } catch {
            onCopyError?.();
            toast.error(t("settingsCopyError"));
          }
        })();
      }}
    >
      <CopyIcon aria-hidden="true" />
      {showLabel && label}
    </Button>
  );
}
