import type { ReactNode } from "react";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";

export function SettingsSection({
  id,
  title,
  detail,
  children,
}: {
  id: string;
  title: string;
  detail: string;
  children: ReactNode;
}) {
  return (
    <section
      className="settings-section"
      id={id}
      aria-labelledby={`${id}-title`}
    >
      <Card>
        <CardHeader>
          <CardTitle id={`${id}-title`} role="heading" aria-level={2}>
            {title}
          </CardTitle>
          <CardDescription>{detail}</CardDescription>
        </CardHeader>
        <CardContent>{children}</CardContent>
      </Card>
    </section>
  );
}
