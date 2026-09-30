import { useEffect, useRef, type ReactNode } from "react";
import {
  Card,
  CardAction,
  CardContent,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import "@/styles/knowledge-panel.css";

interface Props {
  title: string;
  closeLabel: string;
  onClose: () => void;
  children: ReactNode;
}

export function KnowledgePanel({
  title,
  closeLabel,
  onClose,
  children,
}: Props) {
  const panel = useRef<HTMLElement>(null);
  useEffect(() => {
    panel.current?.querySelector<HTMLTextAreaElement>("textarea")?.focus();
  }, []);
  return (
    <section ref={panel} aria-label={title} className="knowledge-panel">
      <Card>
        <CardHeader>
          <CardTitle role="heading" aria-level={2}>
            {title}
          </CardTitle>
          <CardAction>
            <Button type="button" variant="ghost" onClick={onClose}>
              {closeLabel}
            </Button>
          </CardAction>
        </CardHeader>
        <CardContent className="knowledge-panel-content">
          {children}
        </CardContent>
      </Card>
    </section>
  );
}
