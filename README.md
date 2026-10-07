# ContextDepot

[English](README.md) · [简体中文](README.zh-CN.md)

ContextDepot is a self-hosted service that stores structured context and Markdown documents for agents. Agents read and write knowledge through MCP (Model Context Protocol), while a web interface provides browsing, search, and configuration.

Project facts, preferences, decisions, goals, designs, and research notes can be saved and reused across tasks and conversations.

## Features

- **Structured context**: save, retrieve, update, and archive facts, preferences, decisions, goals, states, and events.
- **Markdown documents**: store and read complete designs, research notes, plans, and operating guides.
- **Spaces**: organize context and documents by project or topic.
- **Search**: find context and document excerpts through keyword, semantic, or hybrid retrieval.
- **MCP integration**: load task-related context and save knowledge from compatible agent clients.
- **Access keys**: control which spaces each client can access, update grants, and rotate or revoke keys.
- **Web interface**: browse and read knowledge, create spaces, and manage settings. Supports English and Simplified Chinese, with light and dark themes.

Configure an Embedding model in Inference settings to enable semantic search. Keyword search works without a model provider and remains available if semantic retrieval is unavailable.

## Core concepts

| Concept | Meaning |
| --- | --- |
| Space | A grouping of context and documents for a project or topic; called a Workspace in MCP |
| Context | A reusable piece of knowledge, such as a fact, preference, or decision |
| Document | Longer content stored as a complete Markdown document |
| Access key | A credential that lets an MCP client access authorized spaces |

## Deployment and usage

### 1. Run with Docker

Install Docker and Docker Compose. The `danvic712/context-depot` image includes the web interface and application runtime, so .NET and Bun do not need to be installed separately.

Use [docker-compose.yaml](docker-compose.yaml) and [.env.example](.env.example) from the repository root. You can run them there or copy both files to a separate deployment directory.

Create your local configuration:

```sh
cp .env.example .env
```

In `.env`, set `POSTGRES_PASSWORD` to a long, randomly generated password containing only letters and numbers. You can also adjust these options:

| Variable | Default | Purpose |
| --- | --- | --- |
| `CONTEXTDEPOT_VERSION` | `latest` | Application image tag |
| `HTTP_PORT` | `8080` | Local port for the web interface and MCP |
| `DATA_DIR` | `./data` | Parent directory for persistent data |

Start the services and wait for the application to become healthy:

```sh
docker compose up -d --wait
```

Compose waits for PostgreSQL, runs `database-init` to enable pgvector, and then starts ContextDepot. The initialization service exits after completing its work. ContextDepot creates the required database tables on startup.

Open [http://localhost:8080](http://localhost:8080). If you changed `HTTP_PORT`, use that port in the web and MCP addresses below.

The Compose configuration listens on localhost. For remote access, configure a reachable service address and protect access to the web interface in your deployment.

### 2. Complete the first-run guide

1. **Create a space.** Choose a name and a path, such as `my-project`, and describe what belongs there.
2. **Configure optional model services.** Set up an Embedding model for semantic search, or skip Inference and configure it later.
3. **Create an MCP access key.** Give the key a recognizable name. You can also create keys later in Settings.
4. **Review and finish.** If you created a key, copy it along with the connection details. The full key is shown only once.

The guide saves all changes when you finish. Refreshing the page before then clears the draft.

### 3. Connect your agent

In a compatible MCP client, select **Streamable HTTP**, enter `http://localhost:8080/mcp` as the server address, and set the `X-ContextDepot-Key` request header to your access key.

For clients that use an `mcpServers` configuration, add:

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

Replace `<YOUR_ACCESS_KEY>` with your key. If the client runs on another machine, replace `localhost:8080` with your service's reachable address. Configuration formats vary by client; you can adapt the connection example provided by the setup guide.

In **Settings → Access keys**, grant the key access to each space the client should use. Add a grant whenever you create another space that the client needs to access.

### 4. Save and retrieve knowledge

Ask your connected agent to save or retrieve knowledge with requests like these. Replace `my-project` with your space path and provide the content you want to save:

> In the `my-project` space, remember this preference: review changes before merging and explain important tradeoffs.

> Save this project design as a Markdown document at `project-design.md` in the `my-project` space.

> Before continuing work on `my-project`, retrieve the relevant decisions, preferences, and design documents from ContextDepot.

Browse saved content in **Spaces**, or find context and documents with **Search**. Read the full content on the web, and use your connected agent to update or archive knowledge as the project changes.

## Data and updates

Both services store persistent data under `DATA_DIR`, which defaults to `./data` alongside the Compose file:

| Directory | Contents |
| --- | --- |
| `data/postgresql` | PostgreSQL database files |
| `data/context-depot` | Markdown documents, logs, and encryption keys |

The directories are created on startup. To make a consistent filesystem backup, stop the services before copying the entire data directory. Saved Inference credentials require the original encryption keys.

To stop the containers while keeping the data:

```sh
docker compose down
```

After backing up your data, pull the application image and recreate its container:

```sh
docker compose pull context-depot
docker compose up -d --wait context-depot
```

To pin an application version, set `CONTEXTDEPOT_VERSION` in `.env` to a published release tag. Container logs use automatic rotation, with up to three files of 10 MB each per container.

If the application does not start, view its logs with `docker compose logs context-depot`.

## License

[MIT License](LICENSE)
