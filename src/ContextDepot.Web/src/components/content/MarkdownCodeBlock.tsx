import { Children, isValidElement, useState, type ReactNode } from "react";
import { useTranslation } from "react-i18next";
import { CheckIcon, CopyIcon } from "lucide-react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { writeClipboard } from "@/lib/clipboard";

export function MarkdownCodeBlock({ children }: { children: ReactNode }) {
  const { t } = useTranslation();
  const [copied, setCopied] = useState(false);
  const [pending, setPending] = useState(false);
  const code = Children.toArray(children).find(
    isValidElement<{ children?: ReactNode; className?: string }>,
  );
  const text =
    code && typeof code.props.children === "string" ? code.props.children : "";
  const language = code?.props.className?.match(/language-([\w+-]+)/)?.[1];
  async function copy() {
    if (pending) return;
    setPending(true);
    try {
      await writeClipboard(text);
      setCopied(true);
    } catch {
      toast.error(t("dialogCopyError"));
    } finally {
      setPending(false);
    }
  }
  return (
    <div className="markdown-code-block">
      <div className="markdown-code-toolbar">
        <span>{language}</span>
        <Button
          variant="ghost"
          size="sm"
          disabled={pending}
          onClick={() => void copy()}
          aria-label={t("markdownCopyCode")}
        >
          {copied ? (
            <CheckIcon aria-hidden="true" />
          ) : (
            <CopyIcon aria-hidden="true" />
          )}
          {t(copied ? "dialogCopied" : "markdownCopyCode")}
        </Button>
      </div>
      <pre>{children}</pre>
    </div>
  );
}
