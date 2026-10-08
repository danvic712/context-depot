import { useEffect, useRef, useState } from "react";
import { isAxiosError } from "axios";
import {
  ArrowRightIcon,
  FolderPlusIcon,
  SlidersHorizontalIcon,
  KeyRoundIcon,
  ListChecksIcon,
} from "lucide-react";
import { useTranslation } from "react-i18next";
import {
  useBlocker,
  useNavigate,
  useOutletContext,
  useSearchParams,
} from "react-router";
import { Button } from "@/components/ui/button";
import { SubmitButton } from "@/components/content/SubmitButton";
import { RequestFeedback } from "@/components/feedback/RequestFeedback";
import { InferenceSettings } from "@/features/settings/InferenceSettings";
import { useSettingsResource } from "@/features/settings/use-settings-resource";
import { SettingsResourceState } from "@/features/settings/SettingsResourceState";
import { SetupWorkspaceForm } from "@/features/setup/SetupWorkspaceForm";
import { SetupAccessKey } from "@/features/setup/SetupAccessKey";
import { SetupReview } from "@/features/setup/SetupReview";
import { SetupKeyReceipt } from "@/features/setup/SetupKeyReceipt";
import { SetupNavigation } from "@/features/setup/SetupNavigation";
import { SetupStepFooter } from "@/features/setup/SetupStepFooter";
import { useSetupInferenceDraft } from "@/features/setup/use-setup-inference-draft";
import { setupProviderRequests } from "@/features/setup/setup-inference-draft";
import {
  completeSetup,
  currentSetupStep,
  getSetup,
  getSetupInference,
  setupSteps,
  type SetupStatus,
  type SetupStep,
  type SetupWorkspace,
  type SetupCompletion,
} from "@/features/setup/setup-api";
import { setupErrorMessage } from "@/features/setup/setup-error";
import type { AppContext } from "@/hooks/use-app-context";
import type { Messages } from "@/lib/i18n";
import "@/styles/settings.css";

interface SetupContext extends AppContext {
  setup: SetupStatus;
  onSetupChanged: (status: SetupStatus) => void;
}
const headings = {
  workspace: "setupWorkspaceTitle",
  inference: "setupInferenceTitle",
  accessKey: "setupMcpTitle",
  review: "setupReviewTitle",
} as const;
const details = {
  workspace: "setupWorkspaceWhy",
  inference: "setupInferenceWhy",
  accessKey: "setupMcpWhy",
  review: "setupReviewWhy",
} as const;
const stepIcons = {
  workspace: FolderPlusIcon,
  inference: SlidersHorizontalIcon,
  accessKey: KeyRoundIcon,
  review: ListChecksIcon,
};

export function Setup() {
  const { t } = useTranslation();
  const { setup, onSetupChanged } = useOutletContext<SetupContext>();
  const [params, setParams] = useSearchParams();
  const navigate = useNavigate();
  const [workspace, setWorkspace] = useState<SetupWorkspace>(
    () =>
      setup.workspace ?? {
        id: "draft-workspace",
        name: "",
        path: "",
        description: null,
      },
  );
  const [reached, setReached] = useState<SetupStep>("workspace");
  const localStatus: SetupStatus = {
    ...setup,
    state: "inProgress",
    workspace,
    nextStep: reached,
  };
  const step = currentSetupStep(localStatus, params.get("step"));
  const index = setupSteps.indexOf(step),
    StepIcon = stepIcons[step];
  const catalog = useSettingsResource(getSetupInference);
  const inference = useSetupInferenceDraft(catalog.data);
  const [keyEnabled, setKeyEnabled] = useState(false),
    [keyName, setKeyName] = useState("");
  const [keyInvalid, setKeyInvalid] = useState(false);
  const [pending, setPending] = useState(false);
  const [failure, setFailure] = useState<keyof Messages>();
  const [uncertain, setUncertain] = useState(false);
  const [completion, setCompletion] = useState<SetupCompletion>();
  const [lostKey, setLostKey] = useState(false);
  const request = useRef<AbortController | null>(null);
  const busy = useRef(false),
    allowLeave = useRef(false);
  const heading = useRef<HTMLHeadingElement>(null);
  const dirty =
    !!workspace.name ||
    !!workspace.path ||
    !!workspace.description ||
    keyEnabled ||
    !!inference.draft?.credentials.size;
  const unsafe = dirty || pending || !!completion;
  const blocker = useBlocker(
    ({ nextLocation }) =>
      !allowLeave.current && unsafe && nextLocation.pathname !== "/setup",
  );
  useEffect(() => () => request.current?.abort(), []);
  useEffect(() => {
    if (params.get("step") !== step) setParams({ step }, { replace: true });
  }, [params, step, setParams]);
  useEffect(() => {
    heading.current?.focus();
  }, [step]);
  useEffect(() => {
    if (!unsafe) return;
    const protect = (event: BeforeUnloadEvent) => {
      if (allowLeave.current) return;
      event.preventDefault();
      event.returnValue = "";
    };
    window.addEventListener("beforeunload", protect);
    return () => window.removeEventListener("beforeunload", protect);
  }, [unsafe]);
  function go(next: SetupStep) {
    if (pending || completion || uncertain || next === step) return;
    setFailure(undefined);
    setParams(
      { step: next },
      {
        viewTransition:
          typeof document.startViewTransition === "function" &&
          !window.matchMedia("(prefers-reduced-motion: reduce)").matches,
      },
    );
  }
  function advance(next: SetupStep) {
    if (
      next === "review" &&
      keyEnabled &&
      (!keyName.trim() || keyName.trim().length > 200)
    ) {
      setKeyInvalid(true);
      return;
    }
    if (setupSteps.indexOf(next) > setupSteps.indexOf(reached))
      setReached(next);
    go(next);
  }
  function enter(status: SetupStatus, to = "/") {
    allowLeave.current = true;
    inference.clear();
    setCompletion(undefined);
    onSetupChanged(status);
    void navigate(to, { replace: true });
  }
  async function finish() {
    if (busy.current || uncertain || completion || !inference.draft) return;
    busy.current = true;
    setPending(true);
    setFailure(undefined);
    const controller = new AbortController();
    request.current = controller;
    try {
      const result = await completeSetup(
        {
          workspace: {
            name: workspace.name,
            path: workspace.path,
            description: workspace.description ?? "",
          },
          providers: setupProviderRequests(inference.draft),
          accessKeyName: keyEnabled ? keyName.trim() : null,
        },
        controller.signal,
      );
      if (controller.signal.aborted) return;
      inference.clear();
      if (result.accessKey) {
        setCompletion(result);
      } else enter(result.status);
    } catch (error) {
      if (controller.signal.aborted) return;
      // A transport failure may follow a committed transaction. Read status before allowing a retry.
      if (
        !isAxiosError(error) ||
        !error.response ||
        error.response.status >= 500
      )
        setUncertain(true);
      setFailure(
        isAxiosError(error) && error.response?.status === 403
          ? "settingsForbidden"
          : setupErrorMessage(error),
      );
    } finally {
      busy.current = false;
      if (!controller.signal.aborted) setPending(false);
    }
  }
  async function checkCompletion() {
    if (busy.current) return;
    busy.current = true;
    setPending(true);
    const controller = new AbortController();
    request.current = controller;
    try {
      const status = await getSetup(controller.signal);
      if (controller.signal.aborted) return;
      if (status.state === "completed") {
        inference.clear();
        setCompletion({ status, accessKey: null });
        setLostKey(keyEnabled);
      } else {
        setUncertain(false);
        setFailure(undefined);
      }
    } catch {
      if (!controller.signal.aborted) setFailure("setupCheckError");
    } finally {
      busy.current = false;
      if (!controller.signal.aborted) setPending(false);
    }
  }
  return (
    <div className="setup-layout" data-step={step}>
      <SetupNavigation
        status={localStatus}
        step={step}
        disabled={pending || !!completion || uncertain}
        onStep={go}
      />
      <section
        className="setup-content"
        aria-labelledby="setup-heading"
        aria-busy={pending}
      >
        <header className="setup-page-heading">
          <span className="setup-heading-icon">
            <StepIcon aria-hidden="true" />
          </span>
          <div className="setup-heading-copy">
            <div className="setup-heading-meta">
              <p className="setup-eyebrow">
                {t("setupStepCount", {
                  current: index + 1,
                  total: setupSteps.length,
                })}
              </p>
              {(step === "inference" || step === "accessKey") && (
                <span className="setup-optional">{t("settingsOptional")}</span>
              )}
            </div>
            <h1 id="setup-heading" ref={heading} tabIndex={-1}>
              {t(completion ? "setupSavedTitle" : headings[step])}
            </h1>
            <p>{t(completion ? "setupSavedWhy" : details[step])}</p>
          </div>
        </header>
        {blocker.state === "blocked" && (
          <RequestFeedback
            title={t(completion ? "setupKeyWait" : "setupDraftLeaveTitle")}
            description={t(
              completion ? "setupKeyLeaveBlocked" : "setupDraftLeaveWhy",
            )}
            onRetry={() => blocker.reset()}
          />
        )}
        {failure && !uncertain && !completion && (
          <RequestFeedback
            title={t("setupSaveError")}
            description={t(failure)}
          />
        )}
        {uncertain && !completion && (
          <div>
            <RequestFeedback
              title={t("setupCompletionUncertain")}
              description={t(
                failure === "setupCheckError"
                  ? "setupCheckError"
                  : "setupCompletionCheckWhy",
              )}
              pending={pending}
            />
            <Button
              variant="outline"
              disabled={pending}
              onClick={() => void checkCompletion()}
            >
              {t("setupCheckSaved")}
            </Button>
          </div>
        )}
        {completion?.accessKey ? (
          <SetupKeyReceipt
            issued={completion.accessKey}
            mcpPath={setup.mcpPath}
            onAcknowledge={() => enter(completion.status)}
          />
        ) : completion ? (
          <div className="setup-review">
            <p>{t(lostKey ? "setupCompletionKeyLost" : "setupSavedWhy")}</p>
            <Button
              onClick={() =>
                enter(
                  completion.status,
                  lostKey ? "/settings#access-keys" : "/",
                )
              }
            >
              {t(lostKey ? "setupRecoverInSettings" : "setupOpen")}
            </Button>
          </div>
        ) : (
          <>
            {step === "workspace" && (
              <SetupWorkspaceForm
                workspace={workspace}
                pending={pending}
                onDraftChange={(draft) =>
                  setWorkspace({ ...workspace, ...draft })
                }
                onStart={(draft) => {
                  setWorkspace({ ...workspace, ...draft });
                  advance("inference");
                }}
              />
            )}
            {step === "inference" && (
              <>
                <SettingsResourceState
                  resource={catalog}
                  onRetry={catalog.refresh}
                />
                {inference.draft && (
                  <InferenceSettings
                    editor={inference.editor}
                    draftSettings={inference.draft.settings}
                    onChanged={() => {}}
                    note={
                      <p className="setup-note">{t("setupInferenceNote")}</p>
                    }
                  />
                )}
                <SetupStepFooter
                  onBack={() => go("workspace")}
                  disabled={pending}
                >
                  <SubmitButton
                    icon={ArrowRightIcon}
                    type="button"
                    pending={pending}
                    disabled={!inference.draft}
                    label={t("setupContinue")}
                    pendingLabel={t("settingsSaving")}
                    onClick={() => advance("accessKey")}
                  />
                </SetupStepFooter>
              </>
            )}
            {step === "accessKey" && (
              <>
                <SetupAccessKey
                  workspace={workspace}
                  mcpPath={setup.mcpPath}
                  enabled={keyEnabled}
                  name={keyName}
                  invalid={keyInvalid}
                  onEnabled={setKeyEnabled}
                  onName={(name) => {
                    setKeyName(name);
                    setKeyInvalid(false);
                  }}
                />
                <SetupStepFooter
                  onBack={() => go("inference")}
                  disabled={pending}
                >
                  <SubmitButton
                    icon={ArrowRightIcon}
                    type="button"
                    pending={pending}
                    label={t("setupContinue")}
                    pendingLabel={t("settingsSaving")}
                    onClick={() => advance("review")}
                  />
                </SetupStepFooter>
              </>
            )}
            {step === "review" && inference.draft && (
              <SetupReview
                workspace={workspace}
                settings={inference.draft.settings}
                keyName={keyEnabled ? keyName : null}
                pending={pending}
                disabled={uncertain}
                onComplete={() => void finish()}
                onBack={() => go("accessKey")}
              />
            )}
          </>
        )}
      </section>
    </div>
  );
}
