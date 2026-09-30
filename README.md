# ContextDepot

> A self-hosted durable context service for AI agents.

ContextDepot provides AI agents with persistent, structured, and access-controlled context through the Model Context Protocol (MCP).

It combines structured memory with canonical Markdown, allowing agents to retain project knowledge, decisions, preferences, goals, events, and operational documentation across tasks and sessions.

## Why ContextDepot

AI agents often lose important context between tasks. Conversation history is temporary, while project knowledge and long-term decisions need to remain available, searchable, and properly scoped.

ContextDepot provides a durable context layer that separates:

- Structured context for reusable facts, preferences, decisions, goals, states, and events.
- Canonical Markdown for designs, ADRs, runbooks, research, and plans.
- Derived indexes for efficient lexical, semantic, and hybrid retrieval.

Structured context and Markdown remain the source of truth. Chunks and vector indexes are rebuildable retrieval data.

## Core Capabilities

- Persistent context for AI agents and automation clients.
- Stateless Streamable HTTP MCP endpoint.
- Hierarchical Workspaces for knowledge organization.
- Depot-level access keys with explicit Workspace grants.
- Context lifecycle management, including save, update, search, retrieve, and archive.
- Canonical Markdown document storage.
- Lexical, semantic, and hybrid retrieval.
- Automatic lexical fallback when embeddings are unavailable.
- Background repair of derived indexes.
- Source and sensitive-data checks during writes.
- Structured retrieval results that remain data rather than instructions.

## Core Concepts

| Concept | Description |
| --- | --- |
| Depot | The ownership boundary for related resources |
| Workspace | A knowledge and access boundary inside a Depot |
| Context | Reusable structured long-term information |
| Document | Canonical Markdown content stored within a Workspace |
| Access Key | A credential used by an MCP client |
| Workspace Grant | An explicit permission connecting an Access Key to a Workspace |

## MCP Tools

| Tool | Purpose |
| --- | --- |
| `context_bootstrap` | Load task-relevant context and Markdown excerpts |
| `context_save` | Save durable structured context |
| `context_search` | Search structured context and Markdown |
| `context_get` | Retrieve details for a known context item |
| `context_archive` | Archive a context item |
| `document_upsert` | Create or update a canonical Markdown document |
| `document_get` | Read a canonical Markdown document |
| `document_archive` | Archive a Markdown document |
| `workspace_list` | List available Workspaces |
| `workspace_upsert` | Create or update a Workspace |
| `depot_get` | Read the current Depot profile |

## Request Flow

```text
AI Agent
   |
   v
MCP Endpoint
   |
   v
Access Key Authentication
   |
   v
Workspace Access Scope
   |
   v
Structured Context + Canonical Markdown
   |
   v
Lexical / Semantic / Hybrid Retrieval
```

## HTTP Endpoints

| Endpoint | Description |
| --- | --- |
| `/` and frontend routes | Web prototype, served by the ASP.NET Core host |
| `/mcp` | Stateless Streamable HTTP MCP endpoint |
| `/healthz` | Process health check |
| `/readyz` | Dependency and retrieval readiness check |

MCP requests must include:

```text
X-ContextDepot-Key: cdk_<public-part>.<secret-part>
```

## Requirements

- .NET 10
- Bun 1.4.2 for frontend development and builds
- PostgreSQL 17
- pgvector
- A writable Markdown storage location
- An optional embedding provider for semantic retrieval

Embedding is optional. When semantic retrieval is unavailable, ContextDepot continues to support lexical retrieval.

## Web Development and Publishing

Install frontend dependencies, then start the Host with its Web profile:

```sh
bun install --frozen-lockfile --cwd src/ContextDepot.Web
dotnet run --project src/ContextDepot --launch-profile web
```

The Host requires its normal PostgreSQL and storage configuration. The Web profile starts Bun/Vite on port 5173 and redirects the browser from the Host on port 5289. The existing HTTP/HTTPS profiles continue to run the backend independently.

Publish the frontend and Host together:

```sh
dotnet publish src/ContextDepot/ContextDepot.csproj -c Release -p:BuildFrontend=true -o artifacts/publish
```

Frontend assets are built into the Host's generated `wwwroot` directory and included in the publish output. Production only requires ASP.NET Core. The Docker build also builds and packages the frontend. The Web interface remains a clearly labeled prototype until authorized Web data and browser authentication APIs are available.

## Security Model

Access keys are stored using a public key prefix and a cryptographic hash rather than the original secret.

Each request is restricted to the Workspaces explicitly granted to its Access Key. Retrieved context is returned as application data and does not receive instruction priority.

## Project Status

ContextDepot is under active development.

The current implementation focuses on durable context storage, Workspace-scoped access, MCP integration, retrieval, and reliable degradation when semantic dependencies are unavailable.

## License

[MIT License](LICENSE)
