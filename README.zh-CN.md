# ContextDepot

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

会话记忆会在后台处理提交的对话，保存有用户依据、值得长期保留的知识。该功能**默认关闭**，目前支持 Codex。显式保存和普通检索不需要开启它。

**自动选择空间仍处于预览阶段。** 真实模型的质量评测和安装版 Codex 的端到端验收尚未完成。请在了解这些限制后试用。

### 配置对话捕获

1. 在**设置 → 模型服务**中配置**对话模型（Chat）**。如需通过向量查找候选空间，还要配置搜索模型。
2. 在**设置 → 访问密钥**中，为 Codex 使用的密钥授权目标已有空间。给这些空间填写清晰的名称和描述。自动捕获不会创建空间或授予权限。
3. 打开**设置 → 会话记忆**并启用。此开关对整个应用生效，只是允许接收捕获；还需要让 Codex 提交对话。
4. 使用支持原生 [`mcp_tool` Hook](https://developers.openai.com/codex/hooks/#configure-an-mcp-tool-hook) 的 Codex。在 `~/.codex/config.toml` 中配置或复用 MCP 连接：

   ```toml
   [mcp_servers.context-depot]
   url = "https://context-depot.example.com/mcp"
   env_http_headers = { "X-ContextDepot-Key" = "CONTEXTDEPOT_MCP_TOKEN" }
   ```

   替换地址，通过常用的安全环境配置方式提供 `CONTEXTDEPOT_MCP_TOKEN`，确保 Codex 能读取它。不要把密钥写入 Hook 文件或提交到版本库。
5. 将 [hooks.example.json](scripts/codex/hooks.example.json) 中的 `UserPromptSubmit` 和 `Stop` 条目合并到 `~/.codex/hooks.json`，或受信任项目的 `.codex/hooks.json`。两个 `server` 值都必须与 MCP 连接名一致，这里是 `context-depot`。通过 Codex `/hooks` 审查并信任定义，确认 MCP 已连接。
6. 开启**新对话**。如果使用过旧命令适配器，先移除它的 Hook 注册，不要在一轮对话中途切换捕获方式。由 Hook 捕获时，关闭 Skill 对同一轮对话的重复捕获；Skill 的检索和显式保存仍可使用。

这些 Hook 直接调用 MCP，**不需要 Python、JavaScript 或 Shell 运行时**。它们提交可见的用户提问和助手回复，以及原生会话、回合标识，不提交隐藏推理、工具输出或转录文件。

### 确认保存结果

完成一次包含明确事实或决策、且希望保留的对话。如果取得了返回的 `receiptId` 或 `turnReceiptId`，可以让助手选择其中一个查询 `session_capture_status`。`accepted` 只表示已入队，**不表示已保存**。结果为 `saved` 时，用返回的 Context ID 调用 `context_get`，检查空间、摘要和支持它的用户原文。也可以在网页中浏览或搜索目标空间。

并非每次对话都会生成记忆：没有长期价值，或无法明确匹配已授权空间时，都可能不保存。助手自己的解释不能单独作为保存事实的依据。

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
- **没有生成记忆：** 检查服务端开关、Codex Hook、对话模型、MCP 连接和空间授权。收到入队回执不等于保存成功。

## 许可证

[MIT License](LICENSE)
