import type { KnowledgeSummary, WorkspaceSummary } from "./home-api";
// Only rendered when the URL explicitly requests preview=1.
const now = new Date().toISOString();
export const previewWorkspaces: WorkspaceSummary[] = [
  {
    id: "projects",
    name: "Projects",
    description: "项目决策与进行中的工作",
    path: "projects",
    contextCount: 12,
    documentCount: 4,
    activityAt: now,
  },
  {
    id: "personal",
    name: "Personal",
    description: "偏好、习惯与值得记住的想法",
    path: "personal",
    contextCount: 8,
    documentCount: 2,
    activityAt: now,
  },
  {
    id: "research",
    name: "Research",
    description: "探索、阅读与研究笔记",
    path: "research",
    contextCount: 6,
    documentCount: 3,
    activityAt: now,
  },
];
export const previewKnowledge: KnowledgeSummary[] = [
  {
    id: "decision",
    type: "context",
    kind: "decision",
    title: "将项目决策保存在同一知识来源",
    workspace: { id: "projects", path: "projects" },
    updatedAt: new Date(Date.now() - 7200000).toISOString(),
    indexStatus: null,
  },
  {
    id: "architecture",
    type: "document",
    kind: null,
    title: "ContextDepot 架构设计.md",
    workspace: { id: "projects", path: "projects" },
    updatedAt: new Date(Date.now() - 14400000).toISOString(),
    indexStatus: "indexed",
  },
  {
    id: "preference",
    type: "context",
    kind: "preference",
    title: "界面文案保持简洁清晰",
    workspace: { id: "personal", path: "personal" },
    updatedAt: new Date(Date.now() - 86400000).toISOString(),
    indexStatus: null,
  },
];
