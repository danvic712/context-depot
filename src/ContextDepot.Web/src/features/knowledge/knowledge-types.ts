import { FileTextIcon, MessageSquareTextIcon } from "lucide-react";

export const knowledgeTypes = {
  context: { icon: MessageSquareTextIcon, label: "dialogContexts" },
  document: { icon: FileTextIcon, label: "dialogDocuments" },
} as const;
