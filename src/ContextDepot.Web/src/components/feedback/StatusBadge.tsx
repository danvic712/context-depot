import type { ReactNode } from "react";
import { Badge } from "@/components/ui/badge";
import "@/styles/status.css";

export type StatusTone = "success" | "warning" | "danger" | "neutral";
export function StatusBadge({
  tone,
  children,
}: {
  tone: StatusTone;
  children: ReactNode;
}) {
  return (
    <Badge variant="outline" className="status-badge" data-tone={tone}>
      <span className="status-dot" aria-hidden="true" />
      {children}
    </Badge>
  );
}
