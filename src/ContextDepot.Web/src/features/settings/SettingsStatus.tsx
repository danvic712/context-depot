import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { CheckIcon, CopyIcon } from "lucide-react";
import { useEffect, useRef, useState } from "react";
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
  const [copiedValue, setCopiedValue] = useState<string>();
  const reset = useRef<number | undefined>(undefined);
  const copied = copiedValue === value;
  useEffect(() => () => window.clearTimeout(reset.current), []);
  return (
    <Button
      variant="outline"
      size={showLabel ? "default" : "icon"}
      type="button"
      aria-label={label}
      title={copied ? t("settingsCopied") : label}
      data-copied={copied || undefined}
      onClick={() => {
        window.clearTimeout(reset.current);
        try {
          if (!copyToClipboard(value)) throw new Error("Clipboard unavailable");
          setCopiedValue(value);
          reset.current = window.setTimeout(
            () => setCopiedValue(undefined),
            2000,
          );
        } catch {
          setCopiedValue(undefined);
          onCopyError?.();
          toast.error(t("settingsCopyError"));
        }
      }}
    >
      {copied ? (
        <CheckIcon data-icon="inline-start" aria-hidden="true" />
      ) : (
        <CopyIcon data-icon="inline-start" aria-hidden="true" />
      )}
      {showLabel && label}
      <span className="sr-only" role="status">
        {copied ? t("settingsCopied") : ""}
      </span>
    </Button>
  );
}
