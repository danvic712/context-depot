import { useMemo, useRef, useState, useEffect } from "react";
import type { InferenceProviderSettings } from "@/features/settings/settings-api";
import type { InferenceSettingsEditor } from "@/features/settings/inference-settings-editor";
import {
  createInferenceDraft,
  applySetupProvider,
  applySetupRoute,
  type SetupInferenceDraft,
} from "./setup-inference-draft";

export function useSetupInferenceDraft(
  initial: InferenceProviderSettings | undefined,
) {
  const [draft, setDraft] = useState<SetupInferenceDraft>();
  const latest = useRef<SetupInferenceDraft | undefined>(undefined);
  useEffect(() => {
    if (!initial || latest.current) return;
    latest.current = createInferenceDraft(initial);
    setDraft(latest.current);
  }, [initial]);
  const editor = useMemo<InferenceSettingsEditor>(
    () => ({
      isDraft: true,
      load: async () => latest.current!.settings,
      saveProvider: async (provider, signal) => {
        signal.throwIfAborted();
        latest.current = applySetupProvider(latest.current!, provider);
        setDraft(latest.current);
        return latest.current.settings;
      },
      saveRoute: async (capability, route, signal) => {
        signal.throwIfAborted();
        latest.current = applySetupRoute(latest.current!, capability, route);
        setDraft(latest.current);
        return latest.current.settings.routes.find(
          (item) => item.capability === capability,
        )!;
      },
    }),
    [],
  );
  function clear() {
    latest.current = undefined;
    setDraft(undefined);
  }
  return { draft, editor, clear };
}
