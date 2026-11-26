# ContextDepot

[![发布版本](https://img.shields.io/github/v/release/danvic712/context-depot?include_prereleases&sort=semver)](https://github.com/danvic712/context-depot/releases)
[![构建与测试](https://github.com/danvic712/context-depot/actions/workflows/build-and-test.yml/badge.svg?branch=main)](https://github.com/danvic712/context-depot/actions/workflows/build-and-test.yml?query=branch%3Amain)
[![Docker 镜像构建与发布](https://github.com/danvic712/context-depot/actions/workflows/release-container.yml/badge.svg?event=release)](https://github.com/danvic712/context-depot/actions/workflows/release-container.yml)
[![Docker Hub 拉取量](https://img.shields.io/docker/pulls/danvic712/context-depot)](https://hub.docker.com/r/danvic712/context-depot/tags)
[![许可证](https://img.shields.io/github/license/danvic712/context-depot)](https://github.com/danvic712/context-depot/blob/main/LICENSE)

[English](README.md) · [简体中文](README.zh-CN.md)

ContextDepot 为你的 AI 助手提供一个共享的知识存储。把事实、偏好、决策和 Markdown 文档保存下来，在其他任务或对话中继续使用。助手通过 MCP（模型上下文协议）读写知识，你可以在网页中浏览、搜索和管理这些内容。

本教程带你完成部署、连接助手，并保存第一条知识。使用 Codex 时，还可以按需开启后台会话记忆。

## 开始前准备

- **部署服务：** 安装 Docker 和 Docker Compose。应用镜像已包含网页和运行环境，无需另外安装 .NET 或 Bun。
- **连接助手：** 使用支持 MCP Streamable HTTP 连接的客户端。
- **搜索知识：** 不配置模型也能使用关键词搜索。如需按内容含义搜索，请在**设置 → 模型服务**中添加搜索模型（Embedding）。

**空间**用来归集同一项目或主题的知识，在 MCP 工具中称为 **Workspace**。简短的事实或决策保存为**上下文条目**，长篇笔记保存为 **Markdown 文档**。**访问密钥**决定一个应用可以使用哪些空间。

## 1. 启动 ContextDepot

### 推荐：使用 Docker Compose

使用仓库根目录的 [docker-compose.yaml](docker-compose.yaml) 和 [.env.example](.env.example)，或将两个文件复制到独立的部署目录。在该目录中创建配置文件：

```sh
cp .env.example .env
```

编辑 `.env`，为 `POSTGRES_PASSWORD` 设置较长的随机密码，仅使用英文字母和数字。其他选项可按需调整：

| 变量 | 默认值 | 用途 |
| --- | --- | --- |
| `CONTEXTDEPOT_VERSION` | `latest` | 应用镜像标签；填写已发布的版本标签可固定版本 |
| `HTTP_PORT` | `8080` | 网页和 MCP 使用的端口 |
| `DATA_DIR` | `./data` | 持久化数据目录 |

启动服务：

```sh
docker compose up -d --wait
```

Compose 会启动 PostgreSQL 17、启用 pgvector，再启动 ContextDepot。`database-init` 容器完成初始化后退出属于正常现象。ContextDepot 会在启动时创建所需的数据表。

在部署机器上打开 **http://localhost:8080**；如果修改了 `HTTP_PORT`，请使用对应端口。

**需要从其他设备访问？** Compose 默认只监听本机回环地址。可以通过 HTTPS 反向代理提供访问，也可以将应用的端口映射改为 `"${HTTP_PORT:-8080}:8080"`，在服务器的网络接口上开放端口。请保护网页的访问，MCP 访问密钥不能代替部署层面的访问保护。网页和 `/mcp` 使用同一个可访问的服务地址。

### 其他方式：连接已有 PostgreSQL 数据库

准备已安装 pgvector 的 PostgreSQL 17，以及有权限创建应用数据表的数据库用户。在该数据库中启用扩展：

```sql
CREATE EXTENSION IF NOT EXISTS vector;
```

替换下方的数据库占位符。数据库主机必须能从容器访问；端口不是 `5432` 时也需要修改。在部署目录中运行：

```sh
mkdir -p data/context-depot

docker run -d \
  --name context-depot \
  --restart unless-stopped \
  --publish 8080:8080 \
  --env ASPNETCORE_ENVIRONMENT=Production \
  --env 'ConnectionStrings__ContextDepot=Host=<DB_HOST>;Port=5432;Database=<DB_NAME>;Username=<DB_USER>;Password=<DB_PASSWORD>' \
  --env DataProtection__KeyRingPath=/home/context-depot/keys \
  --mount "type=bind,source=$(pwd)/data/context-depot,target=/home/context-depot" \
  --log-driver local \
  --log-opt max-size=10m \
  --log-opt max-file=3 \
  danvic712/context-depot:latest
```

此命令会在服务器的网络接口上开放 `8080` 端口，请按上文做好访问保护。需要时修改 `--publish 8080:8080` 中的第一个端口；将 `latest` 替换为已发布的版本标签可固定版本。

## 2. 完成首次使用向导

1. **创建空间。** 填写容易识别的名称和标识，例如 `my-project`，并描述这里准备保存什么。
2. **设置搜索，或先跳过。** 搜索模型用于语义搜索，也可以稍后在**设置 → 模型服务**中添加；关键词搜索无需配置模型。
3. **创建访问密钥。** 建议用要连接的应用命名。也可以稍后在**设置 → 访问密钥**中创建。
4. **确认并保存。** 出现完整密钥和连接信息时，请复制并妥善保存。完整密钥只显示一次。

向导会在最后一步统一保存设置。完成前请保持页面打开，不要刷新。

## 3. 连接你的助手

使用 Codex 时，可以[复制配置提示词](#复制提示词让-codex-配置)，让它写入 MCP 配置，并按需添加自动抓取 Hook。

在客户端的 MCP 设置中选择 **Streamable HTTP**，填写：

- **服务地址：** ContextDepot 地址加上 `/mcp`，例如 `https://context-depot.example.com/mcp`。
- **请求头：** 名称为 `X-ContextDepot-Key`，值为你的访问密钥。ContextDepot 不使用 Bearer 认证。

只有客户端与服务运行在同一台机器上时，才使用 `http://localhost:8080/mcp`。远程客户端请填写它能访问的地址，通过网络连接时使用 HTTPS。

对于使用 `mcpServers` 配置的客户端，可以参考：

```json
{
  "mcpServers": {
    "context-depot": {
      "url": "https://context-depot.example.com/mcp",
      "headers": {
        "X-ContextDepot-Key": "<YOUR_ACCESS_KEY>"
      }
    }
  }
}
```

替换地址和 `<YOUR_ACCESS_KEY>`。不同客户端的格式可能不同，请按实际客户端调整，并避免将密钥提交到版本库。

在**设置 → 访问密钥**中，为这把密钥授权目标空间。允许应用创建空间时，只有通过这把密钥新建的空间会自动授权；已有空间和其他方式创建的空间仍需单独授权。

### 可选：安装 ContextDepot Skill

[Skill](skills/context-depot/SKILL.md) 可以指导兼容的助手检索和保存知识。先配置 MCP，再使用 Node.js 和 [skills 安装工具](https://github.com/vercel-labs/skills)安装：

```sh
npx skills add danvic712/context-depot --skill context-depot --global
```

安装目标仅选择当前使用的助手。`--global` 让该助手在不同项目中都能使用此 Skill。从本地仓库安装时，将 `danvic712/context-depot` 替换为 `./skills/context-depot`。

安装 Skill 不会增加空间权限，也不会开启自动捕获。只要助手能够访问 MCP 工具，就可以尝试下面的请求。

## 4. 保存第一条知识

将下面的请求发给已连接的助手，并把 `my-project` 替换为你的空间标识：

> 在 `my-project` 空间记住这项偏好：合并代码前先审查改动，并解释重要的取舍。

打开 ContextDepot 的**空间**页面，确认偏好已出现在目标空间中。然后开启一次新对话，尝试读取：

> 继续 `my-project` 的工作前，先从 ContextDepot 读取相关偏好和决策。

需要保存长篇内容时，提供正文并告诉助手：

> 把这份项目设计保存为 `my-project` 空间中的 Markdown 文档，路径为 `project-design.md`。

通过**搜索**查找已保存的知识。网页用于浏览和阅读；需要更新或归档时，可以让已连接的助手操作。

## 可选：启用 Codex 会话记忆

开启后，Codex 会在提交提问和结束一轮时，把可见的提问、回复和会话标识发给 ContextDepot。后台处理完整回合，复用适合的空间。如果用户明确谈到一个新项目，并且原话中有可保存的长期知识，ContextDepot 可为独立项目创建根级空间，或在匹配的已授权父空间下创建子空间，再保存摘要和必要的原文证据。

### 在 ContextDepot 开启会话记忆

1. 在**设置 → 模型服务**中配置**对话模型（Chat）**。使用向量搜索候选空间时，再配置搜索模型（Embedding）。
2. 在**设置 → 访问密钥**中，为 Codex 的密钥授权要使用的项目空间或父空间。如果希望自动创建独立的根级项目空间，为该密钥开启创建根空间的权限。空间的名称和描述会帮助 ContextDepot 识别项目归属。
3. 在**设置 → 会话记忆**中打开开关。此开关默认关闭，并对整个应用生效。

自动选择空间仍处于预览阶段。真实模型的质量评测和安装版 Codex 的端到端验收尚未完成。

### 复制提示词让 Codex 配置

在要配置的机器上打开一个本地 Codex 聊天，粘贴整段提示词。Codex 会直接从 GitHub 获取 Hook 模板，无需克隆 ContextDepot 仓库。自动抓取需要客户端支持原生 [`mcp_tool` Hook](https://learn.chatgpt.com/docs/hooks#configure-an-mcp-tool-hook)。已有连接时可保留「自动识别」，首次连接请填入实际的 `/mcp` 地址。密钥在本机配置。

```text
请直接在这台机器上为 Codex 配置 ContextDepot。
MCP 地址：自动识别（首次连接请替换为实际的 https://你的服务地址/mcp）
自动抓取范围：当前项目（可改为“全局”或“仅连接 MCP”）

1. 查找实际的 Codex 用户配置目录（CODEX_HOME，默认 ~/.codex）和当前项目根目录。
   检查用户配置及当前受信任项目中与 ContextDepot 有关的 MCP 和 Hook 设置。
   只显示服务器名、文件路径和验证结果，密钥留在本机。
2. 复用在所选范围可用、与 ContextDepot 地址匹配的连接，保留连接名和密钥来源。
   连接有歧义或缺少地址时，询问要用的连接名或实际地址。首次配置时，在用户
   config.toml 新增 [mcp_servers.<连接名>] 并填写实际 url，优先用未被占用的
   context-depot。缺少密钥来源时，添加以下单行 TOML 配置：
   env_http_headers = { "X-ContextDepot-Key" = "CONTEXTDEPOT_MCP_TOKEN" }
   如需补密钥，告诉我如何让 Codex 进程读取该环境变量，或在私有的用户 config.toml
   用 http_headers 设置该请求头。保留其他 MCP 设置。
3. 选择“仅连接 MCP”时跳过 Hook 安装。否则直接从 GitHub 读取 JSON 模板：
   https://raw.githubusercontent.com/danvic712/context-depot/main/scripts/codex/hooks.example.json
   将 UserPromptSubmit 和 Stop 合并到所选范围的 hooks.json。当前项目使用
   <项目根目录>/.codex/hooks.json，全局使用 Codex 用户配置目录下的 hooks.json。
   两处 server 填实际连接名，保留模板 input 和 ${...} 原样。保留其他 Hook，
   跳过已有的相同规则。发现旧 Hook 会重复抓取、连接或 Hook 被禁用，或配置无法解析时，
   告诉我具体冲突，由我决定如何处理。
4. 校验 TOML 和 JSON、适用的 Hook 及其服务器名和范围。报告实际改动路径、
   密钥配置状态和 MCP 连接是否已验证。提示我重启 Codex 以加载配置。若安装了 Hook，
   让我到桌面端“设置 → Hooks”或 CLI 的 /hooks 审查并信任规则。
   按实际验证结果报告 Hook 信任和抓取状态。
```

选择全局时，这台机器上读取该用户配置的本地 Codex 聊天都可能提交对话。由 Hook 抓取后，同一轮对话只保留这一条抓取途径。普通 MCP 检索和显式保存仍可使用。

### 手动配置参考

在 Codex 用户目录的 `config.toml` 中配置或复用 MCP 连接。填写实际可访问的 `/mcp` 地址，并让启动 Codex 的进程读取密钥环境变量。

```toml
[mcp_servers.context-depot]
url = "https://context-depot.example.com/mcp"
env_http_headers = { "X-ContextDepot-Key" = "CONTEXTDEPOT_MCP_TOKEN" }
```

也可在私有的用户 `config.toml` 中用 `http_headers = { "X-ContextDepot-Key" = "<YOUR_ACCESS_KEY>" }` 替换 `env_http_headers` 这一行。在本机编辑密钥，避免把它放进聊天、项目配置或 Hook 文件。

将 [GitHub 上的 Hook 模板](https://github.com/danvic712/context-depot/blob/main/scripts/codex/hooks.example.json) 中的两条规则合并到选定的 `hooks.json`。两处 `server` 要与实际连接名一致，例如有的配置使用 `context_depot`。

从旧版本升级时，更新服务端和数据库即可。已有的 `session_capture` Hook 保留 `workspaceMode: "auto"`，服务端升级后便会按新流程处理。

### 确认保存结果

重启 Codex 以加载配置，再开启新聊天。使用项目级 Hook 时，请在该受信任项目中开启。用 `/mcp` 确认 ContextDepot 已连接且提供 `session_capture`，并在桌面端**设置 → Hooks**或 CLI 的 `/hooks` 中审查并信任两条规则。

说一条明确、希望长期保留的项目决策，带上项目名。若要验证创建子空间，也可以写明它所属的现有类别。完成一轮后，在 ContextDepot 网页的目标空间搜索该决策。处理任务每分钟运行一次，结果可能需要等待几个周期。

如果从 MCP 调用的结构化结果中取到了 `receiptId` 或 `turnReceiptId`，可以任选其一用 `session_capture_status` 查询。`accepted` 表示已入队，`saved` 表示已保存。用返回的 Context ID 调用 `context_get` 可以检查空间、摘要和用户原文。

### 使用时需要了解

- **隐私：** 最小必要的用户原文证据会随知识长期保存，即使临时对话正文已经过期。请在获得相应同意后开启捕获；敏感信息检测无法保证排除所有敏感内容。
- **连接限制：** Hook 需要 MCP 已连接，不会自行重连、重试或离线补发，调用失败可能丢失消息。示例使用五秒活动执行超时，不保证每条消息恰好送达一次。
- **重复回复：** 重复 Stop 事件会去重。首个被接收的助手正文会保留，后续不同正文会冲突，而非替换，不能据此保证捕获到最终修改版。
- **关闭功能：** 不再接收新会话和新捕获，尚未领取的任务暂停；已经领取的任务可能完成。已有记忆保留，清理期限继续计算，重新开启后仅恢复未过期任务。
- **旧部署迁移：** 即使以前使用过 `AgentSessions__Enabled`，也需要在网页中开启，此环境变量已不再控制开关。当前实例直接应用此开关，无需重启；定时配置刷新改为每分钟执行，只运行一个应用实例。

如需使用受管会话的生命周期、检索和状态工具，而非原生 Hook，请参考 [Skill 的 Session Memory 说明](skills/context-depot/SKILL.md#session-memory)。同一轮对话只使用一种捕获工作流。

## 后台处理

仅运行**一个应用实例**。会话捕获、索引修复、应用设置刷新和推理刷新通过同进程的 Hangfire.InMemory 每分钟执行一次。同类任务重叠时跳过，不并发执行。捕获吞吐仍受现有单轮处理上限限制，配置变更可能经过多个分钟周期才被所有后台任务使用。

Hangfire 的定义、排队任务和执行历史只保存在内存中。重启会清空这些状态并重新注册四个定时任务；下一周期扫描 PostgreSQL 中已有的业务队列，中断的业务工作可能等待原租约到期后再继续。不补跑错过的分钟。PostgreSQL 仍用于业务数据，但不新增 Hangfire schema 或数据库权限，也不提供 Hangfire Dashboard。

升级时先停止旧应用进程，再启动新版。启动迁移会删除数据库中废弃的 `IndexRepair:PollIntervalSeconds` 设置。应用忽略 JSON 中的 `ContextDepot:IndexRepair:PollIntervalSeconds` 和环境变量 `ContextDepot__IndexRepair__PollIntervalSeconds`，它们不再影响调度，也无需在启动前手动移除。旧的 `AgentSessions:PollIntervalSeconds` / `AgentSessions__PollIntervalSeconds` 同样被忽略。这些废弃项可从部署配置中清理；其他索引配置名仍严格校验，批次、超时和业务重试约束不变。

若回滚到依赖旧数据库设置的版本，先停止新版并回退这项清理 migration，再启动旧版。回退会恢复历史默认的 30 秒间隔，不覆盖已有设置，也不会恢复删除前的自定义值。保留全部业务数据；无需备份或迁移 Hangfire 内存状态。

## 备份与更新

Workspace 是知识组织的最高层。可以创建独立的根空间，也可以通过子空间组织相关知识。每把访问密钥分别获得 Workspace 授权。

如果旧数据库仍有 Depot 表，请先备份 PostgreSQL 和 Markdown 目录。升级会移除 Depot 表，并保留现有密钥、Workspace ID、授权和原位置的 Markdown 文件。如果旧数据库有多个 Depot，迁移会停止。若需回退到仍使用这些表的版本，请恢复升级前的备份。

Compose 将持久化数据保存在 `DATA_DIR` 下，默认是 `./data`：

| 目录 | 内容 |
| --- | --- |
| `data/postgresql` | PostgreSQL 数据库文件 |
| `data/context-depot` | Markdown 文档、日志和加密密钥 |

使用文件复制方式备份时，先停止服务，再复制整个数据目录以保持一致：

```sh
docker compose down
# 继续之前，先备份数据目录。
docker compose pull context-depot
docker compose up -d --wait context-depot
```

请保留加密密钥，读取已保存的模型服务凭据需要原有密钥。如需固定版本，在 `.env` 中将 `CONTEXTDEPOT_VERSION` 设置为已发布的版本标签。两种部署示例都启用容器日志轮转，每个容器最多保留 3 个 10 MB 日志文件。

通过 `docker run` 部署时，除 `data/context-depot` 外，还需单独备份外部数据库。使用 `docker stop context-depot` 停止应用，拉取目标镜像后，用 `docker rm context-depot` 删除已停止的容器，再使用相同数据目录重新执行原来的启动命令。

## 常见问题

- **应用无法启动：** 运行 `docker compose logs context-depot database-init`；独立容器使用 `docker logs context-depot`。
- **客户端无法连接：** 确认客户端可以访问服务地址，地址以 `/mcp` 结尾，并通过 `X-ContextDepot-Key` 请求头传入有效密钥。
- **某个空间不可用：** 在**设置 → 访问密钥**中检查该空间是否单独授权。父空间的访问权限不会自动授予子空间。
- **无法按含义搜索：** 在**设置 → 模型服务**中检查搜索模型。关键词搜索仍然可用。
- **没有生成记忆：** 先用 `/mcp` 看连接，再在 Hooks 设置中确认两条规则已信任。检查会话记忆开关、对话模型和空间授权，然后[验证保存结果](#确认保存结果)。

## 许可证

[MIT License](LICENSE)
