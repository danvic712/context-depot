// Explicit design fixtures. These records never come from the server.
export const sampleSpaces = [
  {
    id: "projects",
    name: "Projects",
    description: "Plans and decisions for ongoing work.",
    path: "projects",
  },
  {
    id: "personal",
    name: "Personal",
    description: "Preferences and ideas worth keeping.",
    path: "personal",
  },
] as const;

export const sampleKnowledge = [
  {
    id: "decision",
    type: "context",
    kind: "Decision",
    title: "Choose a single source for decisions",
    summary:
      "Keep current decisions in structured context and preserve older versions for review.",
    workspaceId: "projects",
    workspace: "Projects",
    body: "The current decision belongs in structured context. When the decision changes, the previous version remains available as history rather than appearing as current knowledge.",
  },
  {
    id: "preference",
    type: "context",
    kind: "Preference",
    title: "Keep interface copy concise",
    summary:
      "Use clear language and show the next action near the relevant content.",
    workspaceId: "personal",
    workspace: "Personal",
    body: "Prefer short, direct interface copy. Put recovery actions beside the error or empty state they address.",
  },
  {
    id: "architecture",
    type: "document",
    kind: "Document",
    title: "Architecture overview.md",
    summary: "A short guide to the system boundaries and knowledge sources.",
    workspaceId: "projects",
    workspace: "Projects",
    body: "# Architecture overview\n\nStructured context lives in PostgreSQL. Canonical documents are Markdown files. Search indexes can be rebuilt from those sources.\n\nThe Web interface reads through an authorized API.",
  },
  {
    id: "notes",
    type: "document",
    kind: "Document",
    title: "Working notes.md",
    summary: "How to organize ideas before they become durable knowledge.",
    workspaceId: "personal",
    workspace: "Personal",
    body: "# Working notes\n\nKeep useful notes close to the work. Move settled decisions into structured context when they become current truth.",
  },
] as const;

export type SampleKnowledge = (typeof sampleKnowledge)[number];
export function sampleHref(item: SampleKnowledge) {
  return `/${item.type === "context" ? "contexts" : "documents"}/${item.id}`;
}
