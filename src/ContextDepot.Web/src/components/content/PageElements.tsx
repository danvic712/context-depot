import "@/styles/page-elements.css";
import type { ReactNode } from "react";
import type { LucideIcon } from "lucide-react";
import {
  Empty,
  EmptyContent,
  EmptyDescription,
  EmptyHeader,
  EmptyMedia,
  EmptyTitle,
} from "../ui/empty";

export function Notice({
  icon: Icon,
  title,
  detail,
  children,
  heading = false,
}: {
  icon: LucideIcon;
  title: string;
  detail: string;
  children?: ReactNode;
  heading?: boolean;
}) {
  return (
    <Empty className="notice">
      <EmptyHeader>
        <EmptyMedia variant="icon">
          <Icon aria-hidden="true" />
        </EmptyMedia>
        <EmptyTitle>{heading ? <h1>{title}</h1> : title}</EmptyTitle>
        <EmptyDescription>{detail}</EmptyDescription>
      </EmptyHeader>
      {children && <EmptyContent>{children}</EmptyContent>}
    </Empty>
  );
}
