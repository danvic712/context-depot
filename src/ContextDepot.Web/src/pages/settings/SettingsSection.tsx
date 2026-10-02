import type { ReactNode } from "react";
import type { LucideIcon } from "lucide-react";

export function SettingsSection({
  id,
  title,
  detail,
  icon: Icon,
  action,
  children,
}: {
  id: string;
  title: string;
  detail: string;
  icon: LucideIcon;
  action?: ReactNode;
  children: ReactNode;
}) {
  return (
    <section
      className="settings-section"
      id={id}
      aria-labelledby={`${id}-title`}
    >
      <div className="settings-section-header">
        <span className="settings-section-icon">
          <Icon aria-hidden="true" />
        </span>
        <div className="settings-section-heading">
          <h2 id={`${id}-title`}>{title}</h2>
          <p>{detail}</p>
        </div>
        {action && <div className="settings-section-action">{action}</div>}
      </div>
      <div className="settings-section-content">{children}</div>
    </section>
  );
}
