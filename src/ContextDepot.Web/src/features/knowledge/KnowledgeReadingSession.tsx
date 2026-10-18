import {
  useCallback,
  useEffect,
  useLayoutEffect,
  useRef,
  useState,
} from "react";
import { useTranslation } from "react-i18next";
import { ArrowLeftIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Sheet, SheetContent } from "@/components/ui/sheet";
import { useRequestResource } from "@/hooks/use-request-resource";
import { getKnowledgePreview } from "./search-api";
import { resolveDocumentLink } from "./document-link-api";
import { KnowledgeReaderView } from "./KnowledgeReaderView";
import { createReaderSheetMotion } from "./reader-sheet-motion";
import {
  KnowledgeReadingContext,
  useKnowledgeReading,
  type ReadingTarget,
} from "./knowledge-reading-context";

interface ReadingEntry {
  target: ReadingTarget;
  scrollTop?: number;
}

export function KnowledgeReadingSession({
  initial,
  panel = false,
  source,
  search,
  onClose,
}: {
  initial: ReadingTarget;
  panel?: boolean;
  source?: unknown;
  search?: string;
  onClose: (deleted?: boolean) => void;
}) {
  const { t } = useTranslation();
  const outer = useKnowledgeReading();
  const [history, setHistory] = useState<ReadingEntry[]>([{ target: initial }]);
  const entry = history[history.length - 1]!;
  const { target } = entry;
  const [attempt, setAttempt] = useState(0);
  const [open, setOpen] = useState(true);
  const overlay = useRef<HTMLDivElement>(null);
  const motion = useRef<ReturnType<typeof createReaderSheetMotion> | null>(
    null,
  );
  const attachSheet = useCallback((node: HTMLDivElement | null) => {
    motion.current?.dispose();
    motion.current = node
      ? createReaderSheetMotion(node, overlay.current, () => setOpen(false))
      : null;
  }, []);
  const deleted = useRef(false);
  const reader = useRef<HTMLDivElement>(null);
  const load = useCallback(
    async (signal: AbortSignal) => {
      const id =
        target.path === undefined
          ? target.id
          : await resolveDocumentLink(
              target.id,
              target.path,
              target.spaceId,
              signal,
            );
      return getKnowledgePreview(
        { type: target.type, id },
        signal,
        target.spaceId,
      );
    },
    [target],
  );
  const resource = useRequestResource(
    `${target.spaceId ?? "global"}:${target.type}:${target.id}:${target.path ?? ""}`,
    attempt,
    load,
  );
  function scrollPane() {
    return reader.current?.querySelector<HTMLElement>(".knowledge-reader-body");
  }
  function follow(next: ReadingTarget) {
    motion.current?.open();
    const scrollTop = panel ? scrollPane()?.scrollTop : window.scrollY;
    setHistory((current) => [
      ...current.slice(0, -1),
      { ...entry, scrollTop },
      { target: { ...next, spaceId: target.spaceId } },
    ]);
  }
  function close(wasDeleted = false) {
    deleted.current = deleted.current || wasDeleted;
    if (panel) {
      if (motion.current) motion.current.close();
      else setOpen(false);
    } else onClose(wasDeleted);
  }
  const title = resource.error
    ? t("knowledgeLoadError")
    : (resource.data?.title ??
      t(
        target.type === "context"
          ? "contextDetailTitle"
          : "documentReaderTitle",
      ));
  useEffect(() => {
    document.title = `ContextDepot · ${title}`;
  }, [title, t]);
  useLayoutEffect(() => {
    if (!resource.data && !resource.error) return;
    if (entry.scrollTop !== undefined || !target.hash) {
      const top = entry.scrollTop ?? 0;
      const pane = scrollPane();
      if (pane) pane.scrollTo({ top, behavior: "instant" });
      else window.scrollTo({ top, behavior: "instant" });
      reader.current
        ?.querySelector<HTMLHeadingElement>("h1")
        ?.focus({ preventScroll: true });
    }
  }, [resource.data, resource.error, entry, target.hash]);
  const content = (
    <KnowledgeReadingContext
      value={{
        open: follow,
        revision: outer?.revision ?? 0,
        spaceId: target.spaceId,
      }}
    >
      <KnowledgeReaderView
        contentKey={String(history.length)}
        resource={resource}
        source={source}
        search={search}
        spaceId={target.spaceId}
        panel={panel}
        type={target.type}
        titleHint={target.title}
        fragment={entry.scrollTop === undefined ? (target.hash ?? "") : ""}
        onRetry={() => setAttempt((value) => value + 1)}
        onDeleted={() => close(true)}
        navigation={
          history.length > 1 && (
            <Button
              variant="ghost"
              size="sm"
              className="knowledge-reader-previous"
              onClick={() => setHistory((current) => current.slice(0, -1))}
            >
              <ArrowLeftIcon data-icon="inline-start" aria-hidden="true" />
              {t("knowledgePreviousDocument")}
            </Button>
          )
        }
      />
    </KnowledgeReadingContext>
  );
  return panel ? (
    <Sheet
      open={open}
      onOpenChange={(value) => {
        if (!value) close();
      }}
    >
      <SheetContent
        ref={attachSheet}
        overlayProps={{ ref: overlay, className: "knowledge-reader-overlay" }}
        data-reader-motion="spring"
        className="knowledge-reader-sheet"
        showCloseButton={false}
        aria-describedby={undefined}
        onOpenAutoFocus={(event) => {
          event.preventDefault();
          reader.current
            ?.querySelector<HTMLHeadingElement>("h1")
            ?.focus({ preventScroll: true });
        }}
        onCloseAutoFocus={(event) => {
          event.preventDefault();
          // Radix also restores focus when an open sheet is unmounted by
          // route navigation. That cleanup must not navigate back again.
          if (!open) onClose(deleted.current);
        }}
      >
        <div className="knowledge-reader-session" ref={reader}>
          {content}
        </div>
      </SheetContent>
    </Sheet>
  ) : (
    <div
      ref={reader}
      onKeyDown={(event) => {
        if (
          event.key === "Escape" &&
          !event.defaultPrevented &&
          event.currentTarget.contains(event.target as Node)
        ) {
          event.preventDefault();
          close();
        }
      }}
    >
      {content}
    </div>
  );
}
