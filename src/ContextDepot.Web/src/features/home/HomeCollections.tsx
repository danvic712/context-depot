import {
  ArrowRightIcon,
  FileTextIcon,
  FolderIcon,
  MessageSquareTextIcon,
} from "lucide-react";
import { useTranslation } from "react-i18next";
import { Link, useLocation } from "react-router";
import { KnowledgeSkeleton, WorkspaceSkeleton } from "./HomeSkeleton";
import { CreateWorkspaceDialog } from "./CreateWorkspaceDialog";
import type { KnowledgeSummary, WorkspaceSummary } from "./home-api";
import { RequestFeedback } from "@/components/feedback/RequestFeedback";
import type { RequestFailure } from "@/lib/request-failure";
import type { Messages } from "@/lib/i18n";

const kindKeys = {
  fact: "homeKindFact",
  preference: "homeKindPreference",
  decision: "homeKindDecision",
  goal: "homeKindGoal",
  state: "homeKindState",
  event: "homeKindEvent",
  observation: "homeKindObservation",
} as const satisfies Record<string, keyof Messages>;
export function CollectionError({
  onRetry,
  failure,
  title,
  pending = false,
  stale = false,
}: {
  onRetry: () => void | Promise<void>;
  failure?: RequestFailure;
  title: string;
  pending?: boolean;
  stale?: boolean;
}) {
  return (
    <RequestFeedback
      title={title}
      failure={failure}
      onRetry={onRetry}
      pending={pending}
      stale={stale}
      compact={stale}
    />
  );
}
export function WorkspaceTiles({
  items,
  pending,
  empty = false,
  onCreated,
}: {
  items: WorkspaceSummary[];
  pending: boolean;
  empty?: boolean;
  onCreated: () => void | Promise<void>;
}) {
  const { t } = useTranslation();
  const location = useLocation();
  if (empty && !pending)
    return (
      <div className="home-empty-spaces">
        <div className="home-empty-space-intro">
          <span className="home-empty-space-art" aria-hidden="true">
            <FolderIcon />
          </span>
          <div className="home-empty-space-copy">
            <h3>{t("homeSpacesEmptyTitle")}</h3>
            <p>{t("homeSpacesEmpty")}</p>
          </div>
          <CreateWorkspaceDialog onCreated={onCreated} compact />
        </div>
      </div>
    );
  if (pending) return <WorkspaceSkeleton />;
  return (
    <div className="home-space-grid">
      {items.map((space) => (
        <Link
          key={space.id}
          to={`/spaces/${space.id}`}
          className="home-space-tile"
          state={{
            from: location.pathname + location.search,
            navigation: "home",
          }}
        >
          <span className="home-space-icon">
            <FolderIcon aria-hidden="true" />
          </span>
          <strong title={space.name}>{space.name}</strong>
          <span
            className="home-space-description"
            title={space.description ?? undefined}
          >
            {space.description || t("spaceNoDescription")}
          </span>
          <span className="home-space-path" title={space.path}>
            {space.path}
          </span>
          <span className="home-space-meta" title={t("spaceCountsHint")}>
            {t("spaceKnowledgeCounts", {
              contexts: space.contextCount,
              documents: space.documentCount,
            })}
          </span>
        </Link>
      ))}
      <CreateWorkspaceDialog onCreated={onCreated} />
    </div>
  );
}
function relativeTime(timestamp: string, locale: string, now = Date.now()) {
  const seconds = Math.min(0, (Date.parse(timestamp) - now) / 1000);
  const formatter = new Intl.RelativeTimeFormat(locale, { numeric: "auto" });
  if (Math.abs(seconds) < 60) return formatter.format(0, "second");
  const units = [
    [60, "minute"],
    [3600, "hour"],
    [86400, "day"],
    [604800, "week"],
    [2592000, "month"],
    [31536000, "year"],
  ] as const;
  const [scale, unit] =
    [...units].reverse().find(([scale]) => Math.abs(seconds) >= scale) ??
    units[0];
  return formatter.format(Math.round(seconds / scale), unit);
}
export function RecentKnowledge({
  items,
  pending,
}: {
  items: KnowledgeSummary[];
  pending: boolean;
}) {
  const { t, i18n } = useTranslation();
  const location = useLocation();
  const locale = i18n.resolvedLanguage === "zh" ? "zh-CN" : "en-US";
  if (pending) return <KnowledgeSkeleton />;
  return (
    <ul className="home-knowledge-list">
      {items.map((item) => {
        const Icon =
          item.type === "context" ? MessageSquareTextIcon : FileTextIcon;
        return (
          <li key={`${item.type}:${item.id}`}>
            <Link
              to={`/${item.type === "context" ? "contexts" : "documents"}/${item.id}`}
              state={{
                from: location.pathname + location.search,
                navigation: "home",
              }}
              className="home-knowledge-row"
            >
              <span className="home-knowledge-icon">
                <Icon aria-hidden="true" />
              </span>
              <div className="home-knowledge-copy">
                <strong>{item.title}</strong>
                <span>
                  {item.workspace.path}
                  <i aria-hidden="true">·</i>
                  {t(item.kind ? kindKeys[item.kind] : "homeDocument")}
                  {item.indexStatus && item.indexStatus !== "indexed" && (
                    <em>
                      {t(
                        item.indexStatus === "pending"
                          ? "homeIndexPending"
                          : "homeIndexFailed",
                      )}
                    </em>
                  )}
                </span>
              </div>
              <time
                dateTime={item.updatedAt}
                title={new Intl.DateTimeFormat(locale, {
                  dateStyle: "full",
                  timeStyle: "long",
                }).format(new Date(item.updatedAt))}
              >
                {relativeTime(item.updatedAt, locale)}
              </time>
              <ArrowRightIcon className="home-row-arrow" aria-hidden="true" />
            </Link>
          </li>
        );
      })}
    </ul>
  );
}
