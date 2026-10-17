import { knowledgeTypes } from "./knowledge-types";
import { type KnowledgeType } from "@/features/knowledge/knowledge-models";
export function KnowledgeTypeIcon({ type }: { type: KnowledgeType }) {
  const Icon = knowledgeTypes[type].icon;
  return <Icon aria-hidden="true" />;
}
