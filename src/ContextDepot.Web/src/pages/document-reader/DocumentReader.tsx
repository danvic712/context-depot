import { toast } from "sonner";
import { useAppContext } from "@/hooks/use-app-context";
import { useTranslation } from "react-i18next";
import { FileTextIcon } from "lucide-react";
import "@/styles/document-reader.css";
import { useRef, useState } from "react";
import { Notice, Heading } from "@/components/content/PageElements";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Field, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Textarea } from "@/components/ui/textarea";
import { KnowledgePanel } from "@/features/knowledge/KnowledgePanel";
import { MarkdownReader } from "@/features/knowledge/MarkdownReader";
import { DiscardDraft } from "@/features/knowledge/DiscardDraft";

export function DocumentReader() {
  const { preview, item, onBack } = useAppContext();
  const { t } = useTranslation();
  const [isEditing, setIsEditing] = useState(false);
  const [draft, setDraft] = useState(item?.body ?? "");
  const [conflict, setConflict] = useState(false);
  const [showLatest, setShowLatest] = useState(false);
  const editButton = useRef<HTMLButtonElement>(null);
  async function copyDraft() {
    try {
      await navigator.clipboard.writeText(draft);
      toast.success(t("copied"));
    } catch {
      toast.error(t("copyFailed"));
    }
  }
  return (
    <>
      <Button
        type="button"
        variant="ghost"
        className="back-link"
        onClick={onBack}
      >
        ← {t("back")}
      </Button>
      {preview && item?.type === "document" ? (
        <>
          <Heading
            kicker={`${t("sampleDetail")} / ${item.kind}`}
            title={item.title}
            sub={item.summary}
          />
          <p className="path-label">
            {item.workspace} / {item.kind}
          </p>
          <div className="detail-actions">
            <Button
              ref={editButton}
              type="button"
              variant="outline"
              onClick={() => {
                if (isEditing)
                  document.getElementById("document-draft")?.focus();
                else setIsEditing(true);
              }}
            >
              {t("edit")}
            </Button>
          </div>
          <MarkdownReader content={item.body} />
          {isEditing && (
            <KnowledgePanel
              title={t("edit")}
              closeLabel={t("close")}
              onClose={() => {
                setIsEditing(false);
                editButton.current?.focus();
              }}
            >
              <FieldGroup>
                <Field>
                  <FieldLabel htmlFor="document-draft">{t("draft")}</FieldLabel>
                  <Textarea
                    id="document-draft"
                    className="min-h-48"
                    value={draft}
                    onChange={(event) => {
                      setDraft(event.target.value);
                    }}
                    rows={8}
                  />
                </Field>
              </FieldGroup>
              <p>{t("sampleHint")}</p>
              <div className="detail-actions">
                <Button
                  type="button"
                  variant="secondary"
                  onClick={() => setConflict(true)}
                >
                  {t("simulateConflict")}
                </Button>
                <DiscardDraft
                  editorId="document-draft"

                  disabled={draft === item.body}
                  onDiscard={() => {
                    setDraft(item.body);
                    setConflict(false);
                    setShowLatest(false);
                  }}
                />
              </div>
              {conflict && (
                <Alert className="conflict-panel" role="status">
                  <AlertTitle>{t("conflictTitle")}</AlertTitle>
                  <AlertDescription>
                    <p>{t("conflictWhy")}</p>
                    <div className="conflict-actions">
                      <Button
                        type="button"
                        variant="outline"
                        onClick={() => setShowLatest(true)}
                      >
                        {t("latest")}
                      </Button>
                      <Button
                        type="button"
                        variant="outline"
                        onClick={copyDraft}
                      >
                        {t("copyDraft")}
                      </Button>
                    </div>
                    {showLatest && (
                      <>
                        <p>{t("latestWhy")}</p>
                        <MarkdownReader content={item.body} />
                      </>
                    )}
                  </AlertDescription>
                </Alert>
              )}
              <Button type="button" disabled>
                {t("saveUnavailable")}
              </Button>
            </KnowledgePanel>
          )}
        </>
      ) : (
        <Notice
          icon={FileTextIcon}
          title={t("recentMissing")}
          detail={t("recentWhy")}
        />
      )}
    </>
  );
}
