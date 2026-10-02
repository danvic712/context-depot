import type { ReactNode } from "react";
import { Skeleton } from "@/components/ui/skeleton";
import { cn } from "@/lib/utils";
import "@/styles/page-elements.css";

export function PageHeader({
  eyebrow,
  title,
  description,
  actions,
  art,
  variant = "standard",
  children,
}: {
  eyebrow?: ReactNode;
  title: string;
  description: string;
  actions?: ReactNode;
  art?: string;
  variant?: "standard" | "welcome";
  children?: ReactNode;
}) {
  return (
    <header
      className={cn(
        "page-heading",
        variant === "welcome" && "page-heading-welcome",
        art && "page-heading-illustrated",
      )}
    >
      {art && <img className="page-heading-art" src={art} alt="" />}
      <div className="page-heading-copy">
        {eyebrow && <div className="page-eyebrow">{eyebrow}</div>}
        <h1>{title}</h1>
        <p>{description}</p>
        {children}
      </div>
      {actions && <div className="page-heading-actions">{actions}</div>}
    </header>
  );
}

export function PageHeaderSkeleton({ welcome = false }: { welcome?: boolean }) {
  return (
    <div
      className={cn("page-heading", welcome && "page-heading-welcome")}
      aria-hidden="true"
    >
      <div className="page-heading-copy">
        <Skeleton className="h-4 w-28" />
        <Skeleton className="page-title-skeleton" />
        <Skeleton className="h-4 w-3/4" />
      </div>
    </div>
  );
}
