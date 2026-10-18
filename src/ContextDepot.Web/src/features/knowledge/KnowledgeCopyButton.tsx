import { useState } from "react";
import { useTranslation } from "react-i18next";
import { CheckIcon, CopyIcon } from "lucide-react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { writeClipboard } from "@/lib/clipboard";
import { knowledgeKey } from "@/features/knowledge/knowledge-models";
import { type KnowledgePreview } from "@/features/knowledge/knowledge-models";

export function KnowledgeCopyButton({
  detail,
  pending = false,
  iconOnly = false,
}: {
  detail?: KnowledgePreview;
  pending?: boolean;
  iconOnly?: boolean;
}) {
  const { t } = useTranslation();
  const [copied, setCopied] = useState<KnowledgePreview>();
  const [copyPending, setCopyPending] = useState(false);
  const done = !!detail && copied === detail;
  async function copy() {
    if (!detail || copyPending || pending) return;
    setCopyPending(true);
    try {
      await writeClipboard(detail.content);
      setCopied(detail);
      toast.success(t("dialogCopied"), { id: `copy-${knowledgeKey(detail)}` });
    } catch {
      toast.error(t("dialogCopyError"));
    } finally {
      setCopyPending(false);
    }
  }
  return (
    <Button
      variant={iconOnly ? "ghost" : "outline"}
      size={iconOnly ? "icon" : "sm"}
      aria-label={t(done ? "dialogCopied" : "dialogCopy")}
      title={t(done ? "dialogCopied" : "dialogCopy")}
      disabled={!detail || pending || copyPending}
      onClick={() => void copy()}
    >
      {done ? (
        <CheckIcon data-icon="inline-start" aria-hidden="true" />
      ) : (
        <CopyIcon data-icon="inline-start" aria-hidden="true" />
      )}
      {!iconOnly && (
        <span className="knowledge-copy-label">
          {t(done ? "dialogCopied" : "dialogCopy")}
        </span>
      )}
    </Button>
  );
}
