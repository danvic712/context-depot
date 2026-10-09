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
import { ToggleGroup, ToggleGroupItem } from "@/components/ui/toggle-group";
import { RequestFeedback } from "@/components/feedback/RequestFeedback";
import { cn } from "@/lib/utils";
import {
  SearchEmpty,
  SearchSkeleton,
  SearchResultText,
  SearchPreviewContent,
  SearchCopyButton,
} from "./SearchContent";
import { hitKey, type KnowledgeType } from "./search-api";
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
  const [open, setOpen] = useState(true);
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
  const hits = resource.data?.items ?? [];
  const visible = hits.filter((item) => type === "all" || item.type === type);
  const item = visible.find((hit) => hitKey(hit) === selected) ?? visible[0];
  const detail = useKnowledgePreview(item, previewRetry);
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
                          key={hitKey(hit)}
                          value={hitKey(hit)}
                          onSelect={() => {
                            setSelected(hitKey(hit));
                            setMobilePreview(true);
                          }}
                          className="search-result-item"
                        >
                          <KnowledgeTypeIcon type={hit.type} />
                          <SearchResultText hit={hit} query={query} />
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
                    recoveryAction={{
                      label: t("dialogBack"),
                      onClick: () => setMobilePreview(false),
                    }}
                  />
                ) : !detail.data ? (
                  <SearchSkeleton />
                ) : (
                  <>
                    <SearchPreviewContent detail={detail.data} />
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
              onClick={() => onOpenPage({ query, workspace, type })}
            >
              {t("searchOpenPage")}
              <ArrowUpRightIcon data-icon="inline-end" />
            </Button>
            <SearchCopyButton
              detail={detail.error ? undefined : detail.data}
              pending={detail.pending || pending}
            />
          </div>
        </Command>
      </DialogContent>
    </Dialog>
  );
}
