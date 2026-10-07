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

先安装 Docker。`danvic712/context-depot` 镜像已包含网页界面和应用运行环境，无需另外安装 .NET 或 Bun。可以使用 Docker Compose 一起启动应用与数据库，也可以直接运行应用镜像，连接已有数据库。

#### 方式一：Docker Compose

安装 Docker Compose，并使用仓库根目录的 [docker-compose.yaml](docker-compose.yaml) 和 [.env.example](.env.example)。可以直接在仓库根目录运行，也可以将两个文件复制到独立的部署目录。

创建配置文件：

```sh
cp .env.example .env
```

在 `.env` 中为 `POSTGRES_PASSWORD` 设置较长的随机密码，仅使用英文字母和数字。还可以调整以下选项：

| 变量 | 默认值 | 用途 |
| --- | --- | --- |
| `CONTEXTDEPOT_VERSION` | `latest` | 应用镜像的版本标签 |
| `HTTP_PORT` | `8080` | 网页和 MCP 使用的服务器端口 |
| `DATA_DIR` | `./data` | 持久化数据的父目录 |

需要通过服务器 IP 或域名访问时，将 `docker-compose.yaml` 中 `context-depot` 服务的 `ports` 配置替换为：

```yaml
ports:
  - "${HTTP_PORT:-8080}:8080"
```

这样会在服务器的网络接口上开放该端口，请在部署环境中保护网页的访问。如果使用反向代理，请根据实际部署方式配置端口映射。

启动服务，并等待应用通过健康检查：

```sh
docker compose up -d --wait
```

Compose 会等待 PostgreSQL 就绪，运行 `database-init` 启用 pgvector，再启动 ContextDepot。初始化服务完成后退出，ContextDepot 会在启动时创建所需的数据表。

#### 方式二：直接运行镜像

如果已有安装了 pgvector 的 PostgreSQL 17，可以使用此方式。先准备数据库和有权限创建应用数据表的用户，并在该数据库中启用扩展，再启动 ContextDepot：

```sql
CREATE EXTENSION IF NOT EXISTS vector;
```

将下方的 `<DB_HOST>`、`<DB_NAME>`、`<DB_USER>` 和 `<DB_PASSWORD>` 替换为实际数据库连接信息。`<DB_HOST>` 必须是应用容器可以访问的 IP 或主机名；数据库端口不是 `5432` 时，也需要修改对应端口。

在服务器的部署目录中执行：

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

应用会在启动时创建数据表，并将文档、日志和加密密钥保存到 `data/context-depot`。需要固定镜像版本时，将 `latest` 替换为已发布的版本标签。需要修改服务器端口时，调整 `--publish 8080:8080` 中的第一个 `8080`。

#### 访问服务

打开 `http://<SERVER_IP_OR_DOMAIN>:8080`。本文所有网页和 MCP 地址中的 `<SERVER_IP_OR_DOMAIN>` 都需要替换为你的服务器 IP 或域名，例如 `192.168.1.100` 或 `context.example.com`。请使用实际映射的端口；如果反向代理提供 HTTPS，请改用对应的 HTTPS 地址。

### 2. 完成首次使用向导

1. **创建空间。** 设置名称和路径，例如 `my-project`，并描述这里准备保存什么。
2. **按需配置模型服务。** 配置 Embedding 模型以启用语义检索，也可以先跳过 Inference，稍后再设置。
3. **创建 MCP 访问密钥。** 为密钥取一个容易识别的名称，也可以稍后在设置中创建。
4. **确认并完成。** 如果创建了密钥，请复制密钥和连接信息。密钥完整值只显示一次。

向导会在完成时统一保存所有设置。在此之前刷新页面，会清空草稿。

### 3. 连接智能体

在兼容的 MCP 客户端中选择 **Streamable HTTP**，填写服务地址 `http://<SERVER_IP_OR_DOMAIN>:8080/mcp`，并添加请求头 `X-ContextDepot-Key`，值为你的访问密钥。

对于使用 `mcpServers` 配置的客户端，可以添加：

```json
{
  "mcpServers": {
    "contextdepot": {
      "url": "http://<SERVER_IP_OR_DOMAIN>:8080/mcp",
      "headers": {
        "X-ContextDepot-Key": "<YOUR_ACCESS_KEY>"
      }
    }
  }
}
```

将 `<SERVER_IP_OR_DOMAIN>` 替换为你的服务器 IP 或域名，将 `<YOUR_ACCESS_KEY>` 替换为你的密钥。请使用与网页相同的服务地址和端口，如果已配置 HTTPS，也需要使用 HTTPS 地址。不同客户端的配置格式可能有所区别，可以参考首次使用向导提供的连接示例进行调整。

在**设置 → 访问密钥**中，为密钥授权客户端需要使用的空间。之后新建空间时，也需要为相关客户端添加授权。

### 4. 保存和读取知识

连接完成后，可以向智能体发出以下请求。请将 `my-project` 替换为你的空间路径，并提供需要保存的内容：

> 在 `my-project` 空间记住这项偏好：合并代码前先审查改动，并解释重要的取舍。

> 把这份项目设计保存为 `my-project` 空间中的 Markdown 文档，路径为 `project-design.md`。

> 继续 `my-project` 的工作前，先从 ContextDepot 读取相关决策、偏好和设计文档。

在**空间**中浏览已保存的内容，或通过**搜索**查找上下文和文档。网页支持阅读完整内容，项目情况变化时，可以让已连接的智能体更新或归档知识。

## 数据与更新

使用 Docker Compose 部署时，两个服务的持久化数据统一放在 `DATA_DIR` 下，默认是 Compose 文件所在目录的 `./data`：

| 目录 | 保存内容 |
| --- | --- |
| `data/postgresql` | PostgreSQL 数据库文件 |
| `data/context-depot` | Markdown 文档、日志和加密密钥 |

这些目录会在启动时创建。直接运行 `docker run` 示例时，应用数据保存在 `data/context-depot`，外部 PostgreSQL 数据库需要单独备份。使用文件复制方式备份时，请先停止服务，再备份整个数据目录，以保持数据一致。读取已保存的 Inference 凭证需要原有的加密密钥。

使用 Compose 部署时，停止容器并保留数据：

```sh
docker compose down
```

备份数据后，拉取应用镜像并重新创建应用容器：

```sh
docker compose pull context-depot
docker compose up -d --wait context-depot
```

使用 Compose 部署时，如需固定应用版本，在 `.env` 中将 `CONTEXTDEPOT_VERSION` 设置为已发布的版本标签。两种部署示例都启用了容器日志自动轮转，每个容器最多保留 3 个日志文件，每个文件不超过 10 MB。

通过 `docker run` 启动的容器，可以使用 `docker stop context-depot` 停止，使用 `docker logs context-depot` 查看日志。备份数据后，如需更新，先拉取目标版本的镜像，用 `docker rm context-depot` 删除已停止的容器，再使用相同的数据目录重新执行 `docker run` 命令。

使用 Compose 部署时，应用无法启动可通过 `docker compose logs context-depot` 查看日志。

## 许可证

[MIT License](LICENSE)
