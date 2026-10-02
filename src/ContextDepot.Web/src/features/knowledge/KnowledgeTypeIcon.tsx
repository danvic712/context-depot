import { knowledgeTypes } from "./knowledge-types";
import type { KnowledgeType } from "./search-api";
export function KnowledgeTypeIcon({ type }: { type: KnowledgeType }) {
  const Icon = knowledgeTypes[type].icon;
  return <Icon aria-hidden="true" />;
}
