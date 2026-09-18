# P0 Product Value Gate

## 验收命令

代码级回归检查：

```bash
dotnet test ContextDepot.slnx
dotnet build ContextDepot.slnx --no-restore
```

真实 Gate 还需要启动 PostgreSQL 17 与 ContextDepot，连接至少两个 MCP-capable Agent，按 [`p0-dataset.md`](./p0-dataset.md) 的 20 个 Case 各执行 Baseline 与 ContextDepot 两组。Docker Compose 用于提供本地 PostgreSQL 和 Host；在 Docker daemon 不可用时不能把代码级通过写成真实 Gate 通过。

## 目标阈值

| 指标 | P0 目标 |
|---|---:|
| Clear-case Auto Scope accuracy | ≥ 90% |
| Stable-key stale leakage | 0 |
| Cross-agent continuation score ≥ 1 | ≥ 80% cases |
| Bootstrap irrelevant item rate | ≤ 10% |
| Median ContextDepot read calls | ≤ 1 / task |
| Need-write task total calls | ≤ 2 |
| Workspace cross-leak | 0 |
| Markdown silent overwrite | 0 |

## 当前执行记录

截至 2026-09-19：

- `dotnet build ContextDepot.slnx`：通过。
- `dotnet test ContextDepot.slnx`：通过（17 个 Application.Tests）。
- 20 个 Evaluation Case：已建立，真实 Agent 对照结果待执行。
- PostgreSQL / MCP 端到端 Smoke：当前环境 Docker daemon 不可用，未宣称通过。

因此当前状态是 **Implementation Ready / Product Gate Pending**，而不是 Go 结论。执行真实 Case 后把 Baseline、ContextDepot、指标和 P1 调参输入追加到本文档；若阈值未达到，应记录 Rework，而不是用 P1 Semantic Retrieval 掩盖 P0 价值失败。
