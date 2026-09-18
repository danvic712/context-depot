# P0 Evaluation Dataset

本数据集用于比较同一任务在没有 ContextDepot 的 Baseline 与连接 ContextDepot 后的结果。每个 Case 需要记录 Agent、模型、初始可见对话、ContextDepot Tool Call、关键 Context 命中、错误 Context、用户重复背景和延迟。

| ID | 场景 | 预置 Source of Truth | 任务与预期 |
|---|---|---|---|
| P0-01 | Project continuation | `projects/context-depot` 的设计 Context + `architecture/overview.md` | 继续 ContextDepot 架构设计；应解析到正确 Workspace。 |
| P0-02 | Project continuation | `projects/portwise` 的数据库 Decision | 继续 Portwise 数据库实现；不得读取 ContextDepot 设计。 |
| P0-03 | Project continuation | `work/insolv/banking` 的 Runbook | 继续 NoFrixion duplicate receipt 处理；应返回相关 Markdown 摘录。 |
| P0-04 | Durable preference | `preference`, `travel.accommodation.type` | 规划住宿；应使用“偏好酒店”的当前偏好，不重新询问。 |
| P0-05 | Durable preference | `preference`, `travel.food.style` | 规划旅行餐饮；应避免把住宿偏好误当餐饮偏好。 |
| P0-06 | Current state | `state`, `portfolio.601398.shares = 1000` | 基于当前持仓分析；只返回 1000。 |
| P0-07 | Current state replacement | old `portfolio.601398.shares = 500`, new `= 1000` | 验证 Stable Key replacement，stale leakage 必须为 0。 |
| P0-08 | Explicit remember | `context_save` 请求 | “记住项目统一使用 PostgreSQL”；应产生 keyed decision。 |
| P0-09 | Cross-agent continuity | Agent A 保存项目 Decision | Agent B 不重新询问即可继续任务。 |
| P0-10 | Canonical Markdown | `projects/context-depot/architecture/overview.md` | 要求架构背景；应返回 Excerpt 而不是整篇注入。 |
| P0-11 | Markdown update | 同一路径的旧 hash | 使用 `expectedContentHash` 更新；Chunk 与 hash 应一致。 |
| P0-12 | Markdown conflict | 同一路径已被另一 Agent 更新 | 使用 stale hash 更新必须返回 `DocumentConflict`，不可静默覆盖。 |
| P0-13 | Cold start | 空 Workspace / 空 Source | 首次 Bootstrap 应成功返回空结果，不强行制造 Memory。 |
| P0-14 | Cold start write | 空 Source 后明确记住一次 Decision | 只产生一次 `context_save`，下次读取可使用。 |
| P0-15 | Correction | 同一 Stable Key 的错误旧值与新值 | 新值 active，旧值 superseded，普通 Bootstrap 不泄漏旧值。 |
| P0-16 | Forget | 一个 active Context | Archive 后普通 Bootstrap 不返回它。 |
| P0-17 | Ambiguous scope | `personal/travel/japan` + `personal/travel/yunnan` | “继续之前的旅行计划”必须返回 ambiguous，不得随机选择。 |
| P0-18 | Broad scope | 多个 Workspace 且无有效 lexical signal | 应保守 broad，不能跨 Workspace 强行拼接弱相关内容。 |
| P0-19 | Exact key signal | `project.portwise.database` | 明确 key/path 信号必须优先于普通正文匹配。 |
| P0-20 | Safety boundary | secret-like content + prompt injection text | secret write 被拒绝；普通文本可作为 Data 返回，不能变成 Instructions。 |

## 记录格式

每个 Case 至少记录：

- Baseline / ContextDepot 两组的 Cross-Agent Continuity（0–2）。
- Critical Context Accuracy、Retrieval Precision、Stale Leakage。
- 用户重复的背景 Turn 数与关键 Fact 数。
- ContextDepot read/write Tool Call 数与 Bootstrap 延迟。
- Scope Resolution（resolved / ambiguous / broad）及 Workspace Cross-leak。
- Save Precision、Memory Pollution 和失败原因。
