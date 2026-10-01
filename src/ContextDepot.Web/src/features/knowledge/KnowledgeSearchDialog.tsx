import { useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import {
  ArrowLeftIcon,
  CopyIcon,
  CheckIcon,
  FileTextIcon,
  LayersIcon,
  SearchIcon,
  XIcon,
} from "lucide-react";
import { toast } from "sonner";
import {
  Dialog,
  DialogContent,
  DialogClose,
  DialogTitle,
  DialogDescription,
  DialogHeader,
} from "@/components/ui/dialog";
import {
  Command,
  CommandInput,
  CommandList,
  CommandGroup,
  CommandItem,
} from "@/components/ui/command";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { AppSelect } from "@/components/ui/AppSelect";
import { ToggleGroup, ToggleGroupItem } from "@/components/ui/toggle-group";
import {
  Empty,
  EmptyHeader,
  EmptyMedia,
  EmptyTitle,
  EmptyDescription,
} from "@/components/ui/empty";
import { RequestFeedback } from "@/components/feedback/RequestFeedback";
import { Skeleton } from "@/components/ui/skeleton";
import { cn } from "@/lib/utils";
import { MarkdownBody } from "./MarkdownBody";
import { hitKey, type KnowledgeType } from "./search-api";
import {
  useKnowledgeSearch,
  useKnowledgePreview,
  useSearchWorkspaces,
} from "./use-knowledge-search";
import "@/styles/knowledge-search-dialog.css";

function SearchEmpty({
  title,
  description,
}: {
  title: string;
  description: string;
}) {
  return (
    <Empty>
      <EmptyHeader>
        <EmptyMedia variant="icon">
          <SearchIcon aria-hidden="true" />
        </EmptyMedia>
        <EmptyTitle>{title}</EmptyTitle>
        <EmptyDescription>{description}</EmptyDescription>
      </EmptyHeader>
    </Empty>
  );
}
function SearchSkeleton() {
  return (
    <div className="search-skeleton" aria-hidden="true">
      <Skeleton className="h-5 w-2/3" />
      <Skeleton className="h-3 w-1/3" />
      <Skeleton className="h-14 w-full" />
    </div>
  );
}
export default function KnowledgeSearchDialog({
  initialQuery,
  preview,
  onClose,
  onRestoreFocus,
}: {
  initialQuery: string;
  preview: boolean;
  onClose: () => void;
  onRestoreFocus: () => void;
}) {
  const { t, i18n } = useTranslation();
  const [open, setOpen] = useState(true);
  const [workspaceRetry, setWorkspaceRetry] = useState(0);
  const [query, setQuery] = useState(initialQuery);
  const [workspace, setWorkspace] = useState<string>();
  const [type, setType] = useState<"all" | KnowledgeType>("all");
  const [selected, setSelected] = useState("");
  const [mobilePreview, setMobilePreview] = useState(false);
  const [retry, setRetry] = useState(0);
  const [previewRetry, setPreviewRetry] = useState(0);
  const [copiedKey, setCopiedKey] = useState("");
  const [copyPending, setCopyPending] = useState(false);
  const input = useRef<HTMLInputElement>(null);
  const resource = useKnowledgeSearch(query, workspace, preview, retry);
  const workspaces = useSearchWorkspaces(preview, workspaceRetry);
  const hits = resource.data?.items ?? [];
  const visible = hits.filter((item) => type === "all" || item.type === type);
  const item = visible.find((hit) => hitKey(hit) === selected) ?? visible[0];
  const detail = useKnowledgePreview(item, preview, previewRetry);
  const copied = Boolean(detail.data && copiedKey === hitKey(detail.data));
  const pending = resource.pending;
  const counts = {
    all: hits.length,
    context: hits.filter((hit) => hit.type === "context").length,
    document: hits.filter((hit) => hit.type === "document").length,
  };
  const retrySearch = () => setRetry((value) => value + 1);
  function changeQuery(value: string) {
    setQuery(value);
    setMobilePreview(false);
    setCopiedKey("");
  }
  async function copy() {
    if (!detail.data || copyPending) return;
    setCopyPending(true);
    try {
      await navigator.clipboard.writeText(detail.data.content);
      setCopiedKey(hitKey(detail.data));
      toast.success(t("dialogCopied"));
    } catch {
      toast.error(t("dialogCopyError"));
    } finally {
      setCopyPending(false);
    }
  }
  return (
    <Dialog open={open} onOpenChange={setOpen}>
      <DialogContent
        className="knowledge-search-dialog"
        showCloseButton={false}
        overlayClassName="knowledge-search-overlay"
        onOpenAutoFocus={(event) => {
          event.preventDefault();
          input.current?.focus();
        }}
        onCloseAutoFocus={(event) => {
          event.preventDefault();
          onClose();
          onRestoreFocus();
        }}
      >
        <Command
          shouldFilter={false}
          value={item ? hitKey(item) : ""}
          onValueChange={(value) => {
            setSelected(value);
            setCopiedKey("");
          }}
          className="knowledge-search-command"
          label={t("dialogSearchTitle")}
        >
          <DialogHeader className="knowledge-search-header">
            <div className="search-dialog-heading">
              <div>
                <DialogTitle className="text-lg font-semibold">
                  {t("dialogSearchTitle")}
                </DialogTitle>
                <DialogDescription>
                  {t("dialogSearchDescription")}
                </DialogDescription>
              </div>
              <DialogClose asChild>
                <Button
                  variant="outline"
                  size="icon-sm"
                  aria-label={t("dialogClose")}
                  title={t("dialogClose")}
                >
                  <XIcon aria-hidden="true" />
                </Button>
              </DialogClose>
            </div>
            <CommandInput
              ref={input}
              value={query}
              onValueChange={changeQuery}
              placeholder={t("searchPlaceholder")}
              aria-label={t("dialogSearchTitle")}
              maxLength={4000}
            />
          </DialogHeader>
          <div className="search-dialog-filters">
            <ToggleGroup
              type="single"
              variant="outline"
              size="sm"
              value={type}
              onValueChange={(value) => {
                if (value) {
                  setType(value as typeof type);
                  setMobilePreview(false);
                }
              }}
              aria-label={t("typeLabel")}
            >
              {(["all", "context", "document"] as const).map((value) => (
                <ToggleGroupItem key={value} value={value}>
                  {t(
                    value === "all"
                      ? "dialogAll"
                      : value === "context"
                        ? "dialogContexts"
                        : "dialogDocuments",
                  )}
                  {resource.data && (
                    <span className="search-count">{counts[value]}</span>
                  )}
                </ToggleGroupItem>
              ))}
            </ToggleGroup>
            <AppSelect
              label={t("workspaceLabel")}
              value={workspace === undefined ? "all" : `space:${workspace}`}
              disabled={!workspaces.data}
              onChange={(value) => {
                setWorkspace(value === "all" ? undefined : value.slice(6));
                setMobilePreview(false);
              }}
              options={[
                { value: "all", label: t("allSpaces") },
                ...(workspaces.data ?? []).map((space) => ({
                  value: `space:${space.path}`,
                  label: space.path,
                })),
              ]}
            />
          </div>
          {workspaces.error && (
            <RequestFeedback
              className="search-feedback-inline"
              title={t("dialogWorkspaceError")}
              failure={workspaces.error}
              description={
                workspaces.error.retryable
                  ? t("dialogWorkspaceErrorWhy")
                  : undefined
              }
              pending={workspaces.pending}
              onRetry={() => setWorkspaceRetry((value) => value + 1)}
              compact
            />
          )}
          {resource.data?.degraded && (
            <RequestFeedback
              className="search-feedback-inline"
              tone="info"
              title={t("dialogDegradedTitle")}
              description={t("dialogDegraded")}
              compact
            />
          )}
          <div
            className={cn(
              "search-dialog-body",
              mobilePreview && "show-preview",
            )}
          >
            <section
              className="search-dialog-results"
              aria-label={t("dialogResults")}
              aria-busy={pending}
            >
              <div
                className="search-pane-label"
                role="status"
                aria-live="polite"
              >
                {pending
                  ? t("loading")
                  : resource.data
                    ? t("dialogResultCount", { count: visible.length })
                    : t("dialogResults")}
                {preview && <Badge variant="secondary">{t("preview")}</Badge>}
              </div>
              <CommandList
                className="search-result-list"
                label={t("dialogResults")}
              >
                {resource.error ? (
                  <RequestFeedback
                    className="search-feedback-panel"
                    title={t("dialogSearchError")}
                    failure={resource.error}
                    description={
                      resource.error.retryable
                        ? t("dialogSearchErrorWhy")
                        : undefined
                    }
                    onRetry={retrySearch}
                    pending={pending}
                  />
                ) : pending ? (
                  <>
                    <SearchSkeleton />
                    <SearchSkeleton />
                    <SearchSkeleton />
                  </>
                ) : !query.trim() ? (
                  <SearchEmpty
                    title={t("dialogStart")}
                    description={t("dialogStartWhy")}
                  />
                ) : visible.length === 0 ? (
                  <SearchEmpty
                    title={t("dialogNoResults")}
                    description={t("dialogNoResultsWhy")}
                  />
                ) : (
                  <CommandGroup>
                    {visible.map((hit) => (
                      <CommandItem
                        key={hitKey(hit)}
                        value={hitKey(hit)}
                        onSelect={() => {
                          setSelected(hitKey(hit));
                          setMobilePreview(true);
                        }}
                        className="search-result-item"
                      >
                        {hit.type === "context" ? (
                          <LayersIcon aria-hidden="true" />
                        ) : (
                          <FileTextIcon aria-hidden="true" />
                        )}
                        <div className="search-result-text">
                          <strong>{hit.title}</strong>
                          <div className="search-result-meta">
                            <Badge variant="outline">
                              {hit.type === "document"
                                ? t("dialogDocuments")
                                : t(`${hit.kind}Kind`, {
                                    defaultValue: hit.kind,
                                  })}
                            </Badge>
                            <span>{hit.workspace}</span>
                          </div>
                          <p>{hit.excerpt}</p>
                        </div>
                      </CommandItem>
                    ))}
                  </CommandGroup>
                )}
              </CommandList>
              {resource.data && (
                <p className="search-limit-note">{t("dialogLimit")}</p>
              )}
            </section>
            <section
              className="search-dialog-preview"
              aria-label={t("dialogPreview")}
              aria-busy={detail.pending}
            >
              <div className="search-pane-label">
                <span>{t("dialogPreview")}</span>
                <Button
                  className="search-back"
                  variant="outline"
                  size="sm"
                  onClick={() => setMobilePreview(false)}
                >
                  <ArrowLeftIcon data-icon="inline-start" />
                  {t("dialogBack")}
                </Button>
              </div>
              <div className="search-preview-scroll">
                {!item ? (
                  <SearchEmpty
                    title={t("dialogSelect")}
                    description={t("dialogSelectWhy")}
                  />
                ) : detail.error ? (
                  <RequestFeedback
                    title={t("dialogPreviewError")}
                    failure={detail.error}
                    pending={detail.pending}
                    onRetry={() => setPreviewRetry((value) => value + 1)}
                  />
                ) : !detail.data ? (
                  <SearchSkeleton />
                ) : (
                  <>
                    <div className="search-preview-heading">
                      <Badge variant="secondary">
                        {item.type === "document"
                          ? t("dialogDocuments")
                          : t("dialogContexts")}
                      </Badge>
                      <h2>{detail.data.title}</h2>
                      <p>
                        {detail.data.workspace} ·{" "}
                        {new Intl.DateTimeFormat(i18n.resolvedLanguage, {
                          dateStyle: "medium",
                        }).format(new Date(detail.data.updatedAt))}
                      </p>
                    </div>
                    <MarkdownBody content={detail.data.content} />
                  </>
                )}
              </div>
            </section>
          </div>
          <div className="search-dialog-footer">
            <div className="search-key-hints">
              <span>
                <kbd>↑</kbd>
                <kbd>↓</kbd>
                {t("dialogNavigate")}
              </span>
              <span>
                <kbd>↵</kbd>
                {t("dialogPreview")}
              </span>
              <span>
                <kbd>Esc</kbd>
                {t("dialogClose")}
              </span>
            </div>
            <Button
              variant="outline"
              size="sm"
              disabled={!detail.data || copyPending}
              onClick={() => void copy()}
            >
              {copied ? (
                <CheckIcon data-icon="inline-start" />
              ) : (
                <CopyIcon data-icon="inline-start" />
              )}
              {t(copied ? "dialogCopied" : "dialogCopy")}
            </Button>
          </div>
        </Command>
      </DialogContent>
    </Dialog>
  );
}
