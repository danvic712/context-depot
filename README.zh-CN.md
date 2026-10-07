# ContextDepot

[English](README.md) · [简体中文](README.zh-CN.md)

ContextDepot 是一个可自行部署的上下文与文档服务，用于为智能体保存结构化上下文和 Markdown 文档。智能体通过 MCP（Model Context Protocol，模型上下文协议）读写知识，用户可以在网页中浏览、搜索和管理配置。

项目事实、偏好、决策、目标、设计文档和研究笔记都可以保存下来，在后续任务和对话中复用。

## 功能

- **结构化上下文**：保存、读取、更新和归档事实、偏好、决策、目标、状态与事件。
- **Markdown 文档**：保存和阅读完整的设计、研究笔记、计划及操作手册。
- **空间**：按项目或主题组织上下文和文档。
- **搜索**：通过关键词、语义或混合检索查找上下文和文档片段。
- **MCP 集成**：通过兼容的智能体客户端加载任务相关上下文，并保存知识。
- **访问密钥**：控制客户端可访问的空间，支持调整授权、轮换和撤销密钥。
- **网页界面**：浏览和阅读知识、创建空间、管理设置，支持英文、简体中文及浅色和深色主题。

在 Inference 设置中配置 Embedding 模型后，即可启用语义检索。关键词搜索无需配置模型服务，在语义检索不可用时仍然可用。

## 基本概念

| 概念 | 含义 |
| --- | --- |
| 空间 | 归集同一项目或主题的上下文与文档，在 MCP 中称为 Workspace |
| 上下文 | 一条可复用的知识，例如事实、偏好或决策 |
| 文档 | 以完整 Markdown 文档保存的长篇内容 |
| 访问密钥 | 允许 MCP 客户端访问已授权空间的凭证 |

## 部署与使用

### 1. 使用 Docker 启动

先安装 Docker 和 Docker Compose。`danvic712/context-depot` 镜像已包含网页界面和应用运行环境，无需另外安装 .NET 或 Bun。

使用仓库根目录的 [docker-compose.yaml](docker-compose.yaml) 和 [.env.example](.env.example)。可以直接在仓库根目录运行，也可以将两个文件复制到独立的部署目录。

创建本地配置文件：

```sh
cp .env.example .env
```

在 `.env` 中为 `POSTGRES_PASSWORD` 设置较长的随机密码，仅使用英文字母和数字。还可以调整以下选项：

| 变量 | 默认值 | 用途 |
| --- | --- | --- |
| `CONTEXTDEPOT_VERSION` | `latest` | 应用镜像的版本标签 |
| `HTTP_PORT` | `8080` | 网页和 MCP 使用的本机端口 |
| `DATA_DIR` | `./data` | 持久化数据的父目录 |

启动服务，并等待应用通过健康检查：

```sh
docker compose up -d --wait
```

Compose 会等待 PostgreSQL 就绪，运行 `database-init` 启用 pgvector，再启动 ContextDepot。初始化服务完成后退出，ContextDepot 会在启动时创建所需的数据表。

打开 [http://localhost:8080](http://localhost:8080)。如果修改了 `HTTP_PORT`，请在网页和下方的 MCP 地址中使用对应端口。

该 Compose 配置默认只监听本机地址。需要远程访问时，请配置可访问的服务地址，并在部署环境中保护网页的访问。

### 2. 完成首次使用向导

1. **创建空间。** 设置名称和路径，例如 `my-project`，并描述这里准备保存什么。
2. **按需配置模型服务。** 配置 Embedding 模型以启用语义检索，也可以先跳过 Inference，稍后再设置。
3. **创建 MCP 访问密钥。** 为密钥取一个容易识别的名称，也可以稍后在设置中创建。
4. **确认并完成。** 如果创建了密钥，请复制密钥和连接信息。密钥完整值只显示一次。

向导会在完成时统一保存所有设置。在此之前刷新页面，会清空草稿。

### 3. 连接智能体

在兼容的 MCP 客户端中选择 **Streamable HTTP**，填写服务地址 `http://localhost:8080/mcp`，并添加请求头 `X-ContextDepot-Key`，值为你的访问密钥。

对于使用 `mcpServers` 配置的客户端，可以添加：

```json
{
  "mcpServers": {
    "contextdepot": {
      "url": "http://localhost:8080/mcp",
      "headers": {
        "X-ContextDepot-Key": "<YOUR_ACCESS_KEY>"
      }
    }
  }
}
```

将 `<YOUR_ACCESS_KEY>` 替换为你的密钥。如果客户端运行在另一台设备上，请将 `localhost:8080` 替换为可访问的服务地址。不同客户端的配置格式可能有所区别，可以参考首次使用向导提供的连接示例进行调整。

在**设置 → 访问密钥**中，为密钥授权客户端需要使用的空间。之后新建空间时，也需要为相关客户端添加授权。

### 4. 保存和读取知识

连接完成后，可以向智能体发出以下请求。请将 `my-project` 替换为你的空间路径，并提供需要保存的内容：

> 在 `my-project` 空间记住这项偏好：合并代码前先审查改动，并解释重要的取舍。

> 把这份项目设计保存为 `my-project` 空间中的 Markdown 文档，路径为 `project-design.md`。

> 继续 `my-project` 的工作前，先从 ContextDepot 读取相关决策、偏好和设计文档。

在**空间**中浏览已保存的内容，或通过**搜索**查找上下文和文档。网页支持阅读完整内容，项目情况变化时，可以让已连接的智能体更新或归档知识。

## 数据与更新

两个服务的持久化数据统一放在 `DATA_DIR` 下，默认是 Compose 文件所在目录的 `./data`：

| 目录 | 保存内容 |
| --- | --- |
| `data/postgresql` | PostgreSQL 数据库文件 |
| `data/context-depot` | Markdown 文档、日志和加密密钥 |

这些目录会在启动时创建。使用文件复制方式备份时，请先停止服务，再备份整个数据目录，以保持数据一致。读取已保存的 Inference 凭证需要原有的加密密钥。

停止容器并保留数据：

```sh
docker compose down
```

备份数据后，拉取应用镜像并重新创建应用容器：

```sh
docker compose pull context-depot
docker compose up -d --wait context-depot
```

需要固定应用版本时，在 `.env` 中将 `CONTEXTDEPOT_VERSION` 设置为已发布的版本标签。容器日志会自动轮转，每个容器最多保留 3 个日志文件，每个文件不超过 10 MB。

应用无法启动时，可通过 `docker compose logs context-depot` 查看日志。

## 许可证

[MIT License](LICENSE)
