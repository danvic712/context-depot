import { KnowledgeCopyButton } from "./KnowledgeCopyButton";
import { KnowledgeTypeIcon } from "./KnowledgeTypeIcon";
import { useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { ArrowLeftIcon, XIcon, ArrowUpRightIcon } from "lucide-react";
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
import { AppSelect } from "@/components/ui/AppSelect";
import { SearchDialogTypeFilter } from "./SearchDialogTypeFilter";
import { RequestFeedback } from "@/components/feedback/RequestFeedback";
import { cn } from "@/lib/utils";
import { useSearchDialogMotion } from "./use-search-dialog-motion";
import { SearchDialogPreview } from "./SearchDialogPreview";
import { SearchEmpty, SearchSkeleton, SearchResultText } from "./SearchContent";
import { knowledgeKey } from "@/features/knowledge/knowledge-models";
import { type KnowledgeType } from "@/features/knowledge/knowledge-models";
import {
  useKnowledgeSearch,
  useKnowledgePreview,
  useSearchWorkspaces,
} from "./use-knowledge-search";
import "@/styles/knowledge-search-dialog.css";

export default function KnowledgeSearchDialog({
  initialQuery,
  onClose,
  onRestoreFocus,
  onOpenPage,
}: {
  initialQuery: string;
  onClose: () => void;
  onRestoreFocus: () => void;
  onOpenPage: (criteria: {
    query: string;
    workspace?: string;
    type: "all" | KnowledgeType;
  }) => void;
}) {
  const { t } = useTranslation();
  const [workspaceRetry, setWorkspaceRetry] = useState(0);
  const [query, setQuery] = useState(initialQuery);
  const [workspace, setWorkspace] = useState<string>();
  const [type, setType] = useState<"all" | KnowledgeType>("all");
  const [selected, setSelected] = useState("");
  const [mobilePreview, setMobilePreview] = useState(false);
  const [retry, setRetry] = useState(0);
  const [previewRetry, setPreviewRetry] = useState(0);
  const input = useRef<HTMLInputElement>(null);
  const resource = useKnowledgeSearch(query, workspace, retry);
  const workspaces = useSearchWorkspaces(workspaceRetry);
  const expanded = !!query.trim() || !!workspaces.error;
  const {
    ref: dialogMotionRef,
    overlayRef,
    open: reopen,
    close: closeDialog,
  } = useSearchDialogMotion(expanded, onClose);
  const workspaceNames = new Map(
    (workspaces.error?.retryable === false ? [] : (workspaces.data ?? [])).map(
      (space) => [space.path, space.name],
    ),
  );
  const hits = resource.data?.items ?? [];
  const visible = hits.filter((item) => type === "all" || item.type === type);
  const item =
    visible.find((hit) => knowledgeKey(hit) === selected) ?? visible[0];
  const detail = useKnowledgePreview(item, previewRetry);
  const pending = resource.pending;
  const counts = {
    all: hits.length,
    context: hits.filter((hit) => hit.type === "context").length,
    document: hits.filter((hit) => hit.type === "document").length,
  };
  const retrySearch = () => setRetry((value) => value + 1);
  function changeQuery(value: string) {
    reopen();
    setQuery(value);
    setMobilePreview(false);
  }
  function backToResults() {
    setMobilePreview(false);
    input.current?.focus({ preventScroll: true });
  }
  return (
    <Dialog
      open
      onOpenChange={(value) => {
        if (!value) closeDialog();
      }}
    >
      <DialogContent
        ref={dialogMotionRef}
        className="knowledge-search-dialog"
        data-empty={!expanded}
        showCloseButton={false}
        overlayClassName="knowledge-search-overlay"
        overlayProps={{ ref: overlayRef }}
        onPointerDownCapture={() => reopen()}
        onKeyDownCapture={(event) => {
          if (
            (event.metaKey || event.ctrlKey) &&
            event.key.toLowerCase() === "k"
          ) {
            event.preventDefault();
            event.stopPropagation();
            reopen();
            input.current?.focus({ preventScroll: true });
          }
        }}
        onOpenAutoFocus={(event) => {
          event.preventDefault();
          input.current?.focus();
        }}
        onCloseAutoFocus={(event) => {
          event.preventDefault();
          onRestoreFocus();
        }}
      >
        <div
          className="search-dialog-size"
          data-dialog-size="compact"
          aria-hidden="true"
        />
        <div
          className="search-dialog-size"
          data-dialog-size="expanded"
          aria-hidden="true"
        />
        <Command
          shouldFilter={false}
          disablePointerSelection
          value={item ? knowledgeKey(item) : ""}
          onValueChange={(value) => {
            setSelected(value);
          }}
          className="knowledge-search-command"
          label={t("dialogSearchTitle")}
        >
          <DialogHeader className="knowledge-search-header">
            <div className="search-dialog-heading">
              <div>
                <DialogTitle className="search-dialog-title">
                  {t("dialogSearchTitle")}
                </DialogTitle>
                <DialogDescription className="sr-only">
                  {t("dialogSearchDescription")}
                </DialogDescription>
              </div>
              <DialogClose asChild>
                <Button
                  variant="ghost"
                  size="icon-sm"
                  className="search-dialog-close"
                  aria-label={t("dialogClose")}
                  title={t("dialogClose")}
                >
                  <XIcon aria-hidden="true" />
                </Button>
              </DialogClose>
            </div>
            <div className="search-dialog-query">
              <CommandInput
                ref={input}
                value={query}
                onValueChange={changeQuery}
                placeholder={t("searchPlaceholder")}
                aria-label={t("dialogSearchTitle")}
                maxLength={4000}
              />
              {query && (
                <Button
                  type="button"
                  variant="ghost"
                  size="icon-sm"
                  className="search-dialog-clear"
                  aria-label={t("searchClearQuery")}
                  onClick={() => {
                    changeQuery("");
                    input.current?.focus();
                  }}
                >
                  <XIcon aria-hidden="true" />
                </Button>
              )}
            </div>
          </DialogHeader>
          <div className="search-dialog-filters">
            <SearchDialogTypeFilter
              value={type}
              counts={resource.data ? counts : undefined}
              onChange={(value) => {
                setType(value);
                setMobilePreview(false);
              }}
            />
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
                  label: space.name,
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
              stale={!!workspaces.data}
              onRetry={() => setWorkspaceRetry((value) => value + 1)}
              compact
            />
          )}
          {resource.data?.degraded && (
            <RequestFeedback
              className="search-feedback-inline"
              tone="warning"
              title={t("dialogDegradedTitle")}
              description={t("dialogDegraded")}
              compact
            />
          )}
          <div
            data-preview={!!query.trim() && (pending || !!item)}
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
                className={cn("search-pane-label", !query.trim() && "sr-only")}
                role="status"
                aria-live="polite"
              >
                {pending
                  ? t("loading")
                  : resource.data
                    ? t("dialogResultCount", { count: visible.length })
                    : t("dialogResults")}
              </div>
              <CommandList
                className="search-result-list"
                label={t("dialogResults")}
              >
                {resource.error && (
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
                    stale={!!resource.data}
                  />
                )}
                {pending && !resource.data && !resource.error ? (
                  <>
                    <SearchSkeleton />
                    <SearchSkeleton />
                    <SearchSkeleton />
                  </>
                ) : (
                  (!resource.error || resource.data) &&
                  (!query.trim() ? (
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
                          key={knowledgeKey(hit)}
                          value={knowledgeKey(hit)}
                          onSelect={() => {
                            setSelected(knowledgeKey(hit));
                            setMobilePreview(true);
                          }}
                          className="search-result-item"
                        >
                          <KnowledgeTypeIcon type={hit.type} />
                          <SearchResultText
                            hit={hit}
                            query={query}
                            plainMetadata
                            workspaceName={
                              workspaceNames.get(hit.workspace) ??
                              t("searchUnknownSpace")
                            }
                          />
                        </CommandItem>
                      ))}
                    </CommandGroup>
                  ))
                )}
              </CommandList>
              {resource.data && (
                <p className="search-limit-note">
                  {t("searchBatchLimit", { count: resource.data.limit })}
                </p>
              )}
            </section>
            <section
              className="search-dialog-preview"
              aria-label={t("dialogPreview")}
              aria-busy={detail.pending || pending}
            >
              <div className="search-pane-label">
                <span>{t("dialogPreview")}</span>
                <Button
                  className="search-back"
                  variant="outline"
                  size="sm"
                  onClick={backToResults}
                >
                  <ArrowLeftIcon data-icon="inline-start" />
                  {t("dialogBack")}
                </Button>
              </div>
              <SearchDialogPreview
                key={JSON.stringify([query.trim(), workspace, type])}
                item={item}
                detail={detail}
                searchPending={pending}
                workspaceNames={workspaceNames}
                onRetry={() => setPreviewRetry((value) => value + 1)}
                onBack={backToResults}
              />
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
            <div className="search-dialog-actions">
              <Button
                variant="ghost"
                size="sm"
                onClick={() => onOpenPage({ query, workspace, type })}
              >
                {t("searchOpenPage")}
                <ArrowUpRightIcon data-icon="inline-end" />
              </Button>
              {query.trim() && detail.data && !detail.error && (
                <KnowledgeCopyButton
                  detail={detail.data}
                  pending={detail.pending || pending}
                />
              )}
            </div>
          </div>
        </Command>
      </DialogContent>
    </Dialog>
  );
}
