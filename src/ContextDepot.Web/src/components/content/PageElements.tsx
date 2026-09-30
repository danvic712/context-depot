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
}: {
  icon: LucideIcon;
  title: string;
  detail: string;
  children?: ReactNode;
}) {
  return (
    <Empty className="notice">
      <EmptyHeader>
        <EmptyMedia variant="icon">
          <Icon aria-hidden="true" />
        </EmptyMedia>
        <EmptyTitle>{title}</EmptyTitle>
        <EmptyDescription>{detail}</EmptyDescription>
      </EmptyHeader>
      {children && <EmptyContent>{children}</EmptyContent>}
    </Empty>
  );
}
export function Heading({
  kicker,
  title,
  sub,
}: {
  kicker: string;
  title: string;
  sub: string;
}) {
  return (
    <div className="page-heading">
      <span className="kicker">{kicker}</span>
      <h1>{title}</h1>
      <p>{sub}</p>
    </div>
  );
}
