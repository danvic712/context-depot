import {
  responseRecord as object,
  responseText as string,
  responseDate as date,
  responseCount as count,
} from "@/lib/response-values";

function nullableString(value: unknown) {
  return value === null ? null : string(value);
}

export interface WorkspaceSummary {
  id: string;
  name: string;
  description: string | null;
  path: string;
  contextCount: number;
  documentCount: number;
  activityAt: string;
}
export function parseWorkspace(data: unknown): WorkspaceSummary {
  const value = object(data);
  return {
    id: string(value.id),
    name: string(value.name),
    description: nullableString(value.description),
    path: string(value.path),
    contextCount: count(value.contextCount),
    documentCount: count(value.documentCount),
    activityAt: date(value.activityAt),
  };
}
