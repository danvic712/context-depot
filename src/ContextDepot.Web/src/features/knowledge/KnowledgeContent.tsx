import { knowledgeTypes } from "./knowledge-types";
import { useTranslation } from "react-i18next";
import { Badge } from "@/components/ui/badge";
import { MarkdownContent } from "@/components/content/MarkdownContent";
import { type KnowledgePreview } from "@/features/knowledge/knowledge-models";
import "@/styles/knowledge-content.css";

export function KnowledgeContent({
  detail,
  headingLevel = 2,
  showWorkspace = true,
  showHeading = true,
}: {
  detail: KnowledgePreview;
  headingLevel?: 1 | 2;
  showWorkspace?: boolean;
  showHeading?: boolean;
}) {
  const { t } = useTranslation();
  const Title = headingLevel === 1 ? "h1" : "h2";
  return (
    <>
      {showHeading && (
        <header className="knowledge-heading search-preview-heading">
          <Badge variant="secondary">
            {t(knowledgeTypes[detail.type].label)}
          </Badge>
          <Title tabIndex={headingLevel === 1 ? -1 : undefined}>
            {detail.title}
          </Title>
          <KnowledgeMetadata detail={detail} showWorkspace={showWorkspace} />
        </header>
      )}
      <MarkdownContent
        key={`${detail.type}:${detail.id}`}
        content={detail.content}
        nested
      />
    </>
  );
}

export function KnowledgeMetadata({
  detail,
  showWorkspace = true,
  showType = false,
}: {
  detail: KnowledgePreview;
  showWorkspace?: boolean;
  showType?: boolean;
}) {
  const { t, i18n } = useTranslation();
  return (
    <p className="knowledge-metadata">
      {showType && (
        <Badge variant="secondary">
          {t(knowledgeTypes[detail.type].label)}
        </Badge>
      )}
      {showWorkspace && (
        <>
          <span className="knowledge-workspace">{detail.workspace}</span>
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
