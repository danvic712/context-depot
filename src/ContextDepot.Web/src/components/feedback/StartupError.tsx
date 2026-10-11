import { CircleAlertIcon, RotateCwIcon } from "lucide-react";
import { Brand } from "@/components/Brand";
import { SubmitButton } from "@/components/content/SubmitButton";
import {
  Empty,
  EmptyContent,
  EmptyDescription,
  EmptyHeader,
  EmptyMedia,
  EmptyTitle,
} from "@/components/ui/empty";
import type { Lang } from "@/lib/i18n";

export function StartupError({
  title,
  description,
  action,
  pendingLabel,
  pending = false,
  onRetry,
}: {
  title: string;
  description: string;
  action: string;
  pendingLabel: string;
  pending?: boolean;
  onRetry?: () => void | Promise<void>;
}) {
  return (
    <section
      className="startup-error"
      role="alert"
      aria-labelledby="startup-title"
      aria-describedby="startup-description"
    >
      <Empty aria-busy={pending}>
        <EmptyHeader>
          <EmptyMedia variant="icon">
            <CircleAlertIcon aria-hidden="true" strokeWidth={1.4} />
          </EmptyMedia>
          <EmptyTitle id="startup-title" role="heading" aria-level={1}>
            {title}
          </EmptyTitle>
          <EmptyDescription id="startup-description">
            {description}
          </EmptyDescription>
        </EmptyHeader>
        {onRetry && (
          <EmptyContent>
            <SubmitButton
              type="button"
              pending={pending}
              label={action}
              pendingLabel={pendingLabel}
              icon={RotateCwIcon}
              onClick={() => void onRetry()}
            />
          </EmptyContent>
        )}
      </Empty>
    </section>
  );
}

// This screen must remain available when lazy translation resources fail.
const messages = {
  en: {
    title: "Could not open your knowledge space",
    description:
      "The text for this page couldn’t be loaded. Reload the page to try again.",
    action: "Reload page",
    pendingLabel: "Reloading…",
  },
  zh: {
    title: "暂时无法打开知识空间",
    description: "页面文字暂时无法加载，请刷新页面后再试。",
    action: "刷新页面",
    pendingLabel: "正在刷新…",
  },
};

export function AppStartupError({ language }: { language: Lang }) {
  return (
    <div
      className="frame startup-screen"
      lang={language === "zh" ? "zh-CN" : "en"}
    >
      <header className="topbar">
        <div className="brand-lockup">
          <div className="brand-name">
            <Brand />
            <strong>ContextDepot</strong>
          </div>
        </div>
      </header>
      <main className="page startup-screen-content">
        <StartupError
          {...messages[language]}
          onRetry={() => window.location.reload()}
        />
      </main>
    </div>
  );
}
