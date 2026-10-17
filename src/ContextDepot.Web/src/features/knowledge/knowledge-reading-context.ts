import { createContext, useContext } from "react";
import type { KnowledgeType } from "./knowledge-models";

export interface ReadingTarget {
  type: KnowledgeType;
  id: string;
  title?: string;
  spaceId?: string;
  path?: string;
  hash?: string;
}

export const KnowledgeReadingContext = createContext<{
  open: (target: ReadingTarget) => void;
  revision: number;
  spaceId?: string;
} | null>(null);

export function useKnowledgeReading() {
  return useContext(KnowledgeReadingContext);
}

export function isReadingClick(event: {
  button: number;
  metaKey: boolean;
  ctrlKey: boolean;
  shiftKey: boolean;
  altKey: boolean;
  defaultPrevented: boolean;
}) {
  return (
    !event.defaultPrevented &&
    event.button === 0 &&
    !event.metaKey &&
    !event.ctrlKey &&
    !event.shiftKey &&
    !event.altKey
  );
}
