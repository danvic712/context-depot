import { FileTextIcon, MessageSquareTextIcon } from "lucide-react";
import type { ContextKind } from "@/features/home/home-api";
import type { Messages } from "@/lib/i18n";

export const knowledgeKindLabels = {
  fact: "homeKindFact",
  preference: "homeKindPreference",
  decision: "homeKindDecision",
  goal: "homeKindGoal",
  state: "homeKindState",
  event: "homeKindEvent",
  observation: "homeKindObservation",
} as const satisfies Record<ContextKind, keyof Messages>;

export const knowledgeTypes = {
  context: { icon: MessageSquareTextIcon, label: "dialogContexts" },
  document: { icon: FileTextIcon, label: "dialogDocuments" },
} as const;
