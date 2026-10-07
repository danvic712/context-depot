import { isAxiosError } from "axios";
import type { Messages } from "@/lib/i18n";

const errorKeys = {
  SettingsConflict: "setupConflict",
  InvalidInferenceConfiguration: "settingsInferenceInvalid",
  InvalidRequest: "setupSaveError",
  SetupConflict: "setupConflict",
  SetupIncomplete: "setupIncomplete",
  SetupUnavailable: "setupUnavailable",
  SetupRequired: "setupIncomplete",
  InvalidSetupWorkspace: "setupInvalidWorkspace",
  InvalidSetupStep: "setupInvalidStep",
} as const;

export function setupErrorMessage(error: unknown): keyof Messages {
  if (!isAxiosError(error)) return "setupSaveError";
  const data: unknown = error.response?.data;
  if (
    data &&
    typeof data === "object" &&
    "code" in data &&
    typeof data.code === "string" &&
    Object.hasOwn(errorKeys, data.code)
  )
    return errorKeys[data.code as keyof typeof errorKeys];
  return "setupSaveError";
}
