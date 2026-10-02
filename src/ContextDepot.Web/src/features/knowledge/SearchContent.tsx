import { useState } from "react";
import { useTranslation } from "react-i18next";
import { toast } from "sonner";
import { CheckIcon, CopyIcon, SearchIcon } from "lucide-react";
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
import { hitKey, type SearchHit, type KnowledgePreview } from "./search-api";

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
}: {
  hit: SearchHit;
  query?: string;
}) {
  const { t } = useTranslation();
  return (
    <div className="search-result-text">
      <strong>
        <Highlight text={hit.title} query={query} />
      </strong>
      <div className="search-result-meta">
        <Badge variant="outline">
          {t(hit.type === "document" ? "dialogDocuments" : "dialogContexts")}
        </Badge>
        {hit.type === "context" && (
          <span>{t(`${hit.kind}Kind`, { defaultValue: hit.kind })}</span>
        )}
        <span className="search-hit-path" title={hit.workspace}>
          {hit.workspace}
        </span>
      </div>
      <p>
        <Highlight text={hit.excerpt} query={query} />
      </p>
    </div>
  );
}

export function SearchCopyButton({
  detail,
  pending = false,
}: {
  detail?: KnowledgePreview;
  pending?: boolean;
}) {
  const { t } = useTranslation();
  const [copied, setCopied] = useState<KnowledgePreview>();
  const [copyPending, setCopyPending] = useState(false);
  const done = !!detail && copied === detail;
  async function copy() {
    if (!detail || copyPending || pending) return;
    setCopyPending(true);
    try {
      await navigator.clipboard.writeText(detail.content);
      setCopied(detail);
      toast.success(t("dialogCopied"), { id: `copy-${hitKey(detail)}` });
    } catch {
      toast.error(t("dialogCopyError"));
    } finally {
      setCopyPending(false);
    }
  }
  return (
    <Button
      variant="outline"
      size="sm"
      disabled={!detail || pending || copyPending}
      onClick={() => void copy()}
    >
      {done ? (
        <CheckIcon data-icon="inline-start" aria-hidden="true" />
      ) : (
        <CopyIcon data-icon="inline-start" aria-hidden="true" />
      )}
      {t(done ? "dialogCopied" : "dialogCopy")}
    </Button>
  );
}
