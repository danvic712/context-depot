import { CheckIcon, SaveIcon } from "lucide-react";
import { useTranslation } from "react-i18next";
import { setupSteps, type SetupStatus, type SetupStep } from "./setup-api";

export function SetupNavigation({
  status,
  step,
  disabled,
  onStep,
}: {
  status: SetupStatus;
  step: SetupStep;
  disabled: boolean;
  onStep: (step: SetupStep) => void;
}) {
  const { t } = useTranslation();
  const maxIndex = status.workspace ? setupSteps.indexOf(status.nextStep) : 0;
  return (
    <aside className="setup-sidebar">
      <div className="setup-introduction">
        <p className="setup-eyebrow">{t("setupEyebrow")}</p>
        <h2>{t("setupTitle")}</h2>
        <p>{t("setupIntro")}</p>
      </div>
      <nav aria-label={t("setupStepsLabel")}>
        <ol className="setup-steps">
          {setupSteps.map((item, itemIndex) => (
            <li key={item} data-completed={itemIndex < maxIndex}>
              <button
                type="button"
                aria-current={item === step ? "step" : undefined}
                disabled={disabled || itemIndex > maxIndex}
                onClick={() => onStep(item)}
              >
                <span className="setup-step-number">
                  {itemIndex < maxIndex ? (
                    <CheckIcon aria-hidden="true" />
                  ) : (
                    itemIndex + 1
                  )}
                </span>
                <span>
                  <strong>{t(`setupStep_${item}`)}</strong>
                  <small>{t(`setupStepDetail_${item}`)}</small>
                </span>
              </button>
            </li>
          ))}
        </ol>
      </nav>
      <p className="setup-resume">
        <SaveIcon aria-hidden="true" />
        {t("setupResume")}
      </p>
    </aside>
  );
}
