import { knowledgeTypes } from "./knowledge-types";
import { useTranslation } from "react-i18next";
import { Badge } from "@/components/ui/badge";
import { MarkdownContent } from "@/components/content/MarkdownContent";
import { markdownDocumentLink } from "@/lib/markdown-url";
import { DocumentMarkdownLink } from "./DocumentMarkdownLink";
import { type KnowledgePreview } from "@/features/knowledge/knowledge-models";
import "@/styles/knowledge-content.css";

export function KnowledgeContent({
  detail,
  headingLevel = 2,
  showWorkspace = true,
  showHeading = true,
  workspaceName,
  fragment,
}: {
  detail: KnowledgePreview;
  headingLevel?: 1 | 2;
  showWorkspace?: boolean;
  showHeading?: boolean;
  workspaceName?: string;
  fragment?: string;
}) {
  const { t } = useTranslation();
  const Title = headingLevel === 1 ? "h1" : "h2";
  return (
    <>
      {showHeading && (
        <header className="knowledge-heading search-preview-heading">
          <Badge variant="secondary">
            {t(
              detail.type === "document"
                ? "searchDocumentType"
                : knowledgeTypes[detail.type].label,
            )}
          </Badge>
          <Title tabIndex={headingLevel === 1 ? -1 : undefined}>
            {detail.title}
          </Title>
          <KnowledgeMetadata
            detail={detail}
            showWorkspace={showWorkspace}
            workspaceName={workspaceName}
          />
        </header>
      )}
      <MarkdownContent
        key={`${detail.type}:${detail.id}`}
        content={detail.content}
        fragment={fragment}
        nested
        renderLink={
          detail.type === "document"
            ? (href, children) => {
                const link = markdownDocumentLink(href);
                return link ? (
                  <DocumentMarkdownLink sourceId={detail.id} {...link}>
                    {children}
                  </DocumentMarkdownLink>
                ) : undefined;
              }
            : undefined
        }
      />
    </>
  );
}

export function KnowledgeMetadata({
  detail,
  showWorkspace = true,
  showType = false,
  workspaceName,
}: {
  detail: KnowledgePreview;
  showWorkspace?: boolean;
  showType?: boolean;
  workspaceName?: string;
}) {
  const { t, i18n } = useTranslation();
  return (
    <p className="knowledge-metadata">
      {showType && (
        <Badge variant="secondary">
          {t(
            detail.type === "document"
              ? "searchDocumentType"
              : knowledgeTypes[detail.type].label,
          )}
        </Badge>
      )}
      {showWorkspace && (
        <>
          <span className="knowledge-workspace">
            {workspaceName ?? detail.workspaceName ?? t("searchUnknownSpace")}
          </span>
          <span aria-hidden="true">·</span>
        </>
      )}
      <time dateTime={detail.updatedAt}>
        {t("searchUpdated", {
          date: new Intl.DateTimeFormat(i18n.resolvedLanguage, {
            dateStyle: "medium",
          }).format(new Date(detail.updatedAt)),
        })}
      </time>
    </p>
  );
}
