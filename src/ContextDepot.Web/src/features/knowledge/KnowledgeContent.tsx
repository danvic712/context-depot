import { knowledgeTypes } from "./knowledge-types";
import { useTranslation } from "react-i18next";
import { Badge } from "@/components/ui/badge";
import { MarkdownContent } from "@/components/content/MarkdownContent";
import type { KnowledgePreview } from "./search-api";
import "@/styles/knowledge-content.css";

export function KnowledgeContent({
  detail,
  headingLevel = 2,
}: {
  detail: KnowledgePreview;
  headingLevel?: 1 | 2;
}) {
  const { t, i18n } = useTranslation();
  const Title = headingLevel === 1 ? "h1" : "h2";
  return (
    <>
      <header className="knowledge-heading search-preview-heading">
        <Badge variant="secondary">
          {t(knowledgeTypes[detail.type].label)}
        </Badge>
        <Title tabIndex={headingLevel === 1 ? -1 : undefined}>
          {detail.title}
        </Title>
        <p>
          <span className="knowledge-workspace">{detail.workspace}</span>
          <span aria-hidden="true">·</span>
          <time dateTime={detail.updatedAt}>
            {t("searchUpdated", {
              date: new Intl.DateTimeFormat(i18n.resolvedLanguage, {
                dateStyle: "medium",
              }).format(new Date(detail.updatedAt)),
            })}
          </time>
        </p>
      </header>
      <MarkdownContent content={detail.content} nested />
    </>
  );
}
