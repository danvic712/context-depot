import { useAppContext } from "@/hooks/use-app-context";
import { useTranslation } from "react-i18next";
import { FileTextIcon } from "lucide-react";
import "@/styles/context-detail.css";
import { useRef, useState } from "react";
import { Notice, Heading } from "@/components/content/PageElements";
import { Button } from "@/components/ui/button";
import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from "@/components/ui/alert-dialog";
import { Field, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Textarea } from "@/components/ui/textarea";
import { KnowledgePanel } from "@/features/knowledge/KnowledgePanel";
import { MarkdownReader } from "@/features/knowledge/MarkdownReader";
import { DiscardDraft } from "@/features/knowledge/DiscardDraft";

export function ContextDetail() {
  const { preview, item, onBack } = useAppContext();
  const { t } = useTranslation();
  const [isCorrecting, setIsCorrecting] = useState(false);
  const [draft, setDraft] = useState(item?.body ?? "");
  const editButton = useRef<HTMLButtonElement>(null);
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
      {preview && item?.type === "context" ? (
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
                if (isCorrecting)
                  document.getElementById("context-draft")?.focus();
                else setIsCorrecting(true);
              }}
            >
              {t("correction")}
            </Button>
            <AlertDialog>
              <AlertDialogTrigger asChild>
                <Button type="button" variant="outline">
                  {t("archive")}
                </Button>
              </AlertDialogTrigger>
              <AlertDialogContent>
                <AlertDialogHeader>
                  <AlertDialogTitle>{t("archive")}</AlertDialogTitle>
                  <AlertDialogDescription>
                    <strong>{item.title}</strong>
                    <br />
                    {t("archiveWhy")}
                  </AlertDialogDescription>
                </AlertDialogHeader>
                <AlertDialogFooter>
                  <AlertDialogCancel>{t("close")}</AlertDialogCancel>
                  <Button variant="destructive" disabled>
                    {t("archiveUnavailable")}
                  </Button>
                </AlertDialogFooter>
              </AlertDialogContent>
            </AlertDialog>
          </div>
          <MarkdownReader content={item.body} />
          {isCorrecting && (
            <KnowledgePanel
              title={t("reviewCorrection")}
              closeLabel={t("close")}
              onClose={() => {
                setIsCorrecting(false);
                editButton.current?.focus();
              }}
            >
              <FieldGroup>
                <Field>
                  <FieldLabel htmlFor="context-draft">{t("draft")}</FieldLabel>
                  <Textarea
                    id="context-draft"
                    className="min-h-48"
                    value={draft}
                    onChange={(event) => setDraft(event.target.value)}
                    rows={8}
                  />
                </Field>
              </FieldGroup>
              <p>{t("correctionWhy")}</p>
              <div className="detail-actions">
                <DiscardDraft
                  editorId="context-draft"

                  disabled={draft === item.body}
                  onDiscard={() => setDraft(item.body)}
                />
                <Button type="button" disabled>
                  {t("saveUnavailable")}
                </Button>
              </div>
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
