import { knowledgeTypes } from "./knowledge-types";
import { useTranslation } from "react-i18next";
import { SearchIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Skeleton } from "@/components/ui/skeleton";
import {
  Empty,
  EmptyHeader,
  EmptyMedia,
  EmptyTitle,
  EmptyDescription,
  EmptyContent,
} from "@/components/ui/empty";
import { type SearchHit } from "./search-api";

export { KnowledgeContent as SearchPreviewContent } from "./KnowledgeContent";

export function SearchEmpty({
  title,
  description,
  onReset,
}: {
  title: string;
  description: string;
  onReset?: () => void;
}) {
  const { t } = useTranslation();
  return (
    <Empty>
      <EmptyHeader>
        <EmptyMedia variant="icon">
          <SearchIcon aria-hidden="true" />
        </EmptyMedia>
        <EmptyTitle>{title}</EmptyTitle>
        <EmptyDescription>{description}</EmptyDescription>
      </EmptyHeader>
      {onReset && (
        <EmptyContent>
          <Button variant="outline" onClick={onReset}>
            {t("searchReset")}
          </Button>
        </EmptyContent>
      )}
    </Empty>
  );
}

export function SearchSkeleton() {
  return (
    <div className="search-skeleton" aria-hidden="true">
      <Skeleton className="h-5 w-2/3" />
      <Skeleton className="h-3 w-1/3" />
      <Skeleton className="h-14 w-full" />
    </div>
  );
}

function Highlight({ text, query }: { text: string; query: string }) {
  const term = query.trim();
  if (!term) return text;
  const index = text.toLocaleLowerCase().indexOf(term.toLocaleLowerCase());
  if (index < 0) return text;
  return (
    <>
      {text.slice(0, index)}
      <mark>{text.slice(index, index + term.length)}</mark>
      {text.slice(index + term.length)}
    </>
  );
}

export function SearchResultText({
  hit,
  query = "",
  plainMetadata = false,
  workspaceName,
}: {
  hit: SearchHit;
  query?: string;
  plainMetadata?: boolean;
  workspaceName?: string;
}) {
  const { t, i18n } = useTranslation();
  return (
    <div className="search-result-text">
      <strong>
        <Highlight text={hit.title} query={query} />
      </strong>
      <div className="search-result-meta">
        {plainMetadata ? (
          <span>
            {t(
              hit.type === "context" ? `${hit.kind}Kind` : "searchDocumentType",
              { defaultValue: hit.kind },
            )}
          </span>
        ) : (
          <Badge variant="outline">{t(knowledgeTypes[hit.type].label)}</Badge>
        )}
        {!plainMetadata && hit.type === "context" && (
          <span>{t(`${hit.kind}Kind`, { defaultValue: hit.kind })}</span>
        )}
        {plainMetadata && <span aria-hidden="true">·</span>}
        <span
          className="search-hit-space"
          title={workspaceName ?? hit.workspace}
        >
          {workspaceName ?? hit.workspace}
        </span>
        {hit.updatedAt && (
          <time dateTime={hit.updatedAt}>
            {t("searchUpdated", {
              date: new Intl.DateTimeFormat(
                i18n.resolvedLanguage === "zh" ? "zh-CN" : "en-US",
                { month: "short", day: "numeric" },
              ).format(new Date(hit.updatedAt)),
            })}
          </time>
        )}
      </div>
      {hit.excerpt && (
        <p>
          <Highlight text={hit.excerpt} query={query} />
        </p>
      )}
    </div>
  );
}
