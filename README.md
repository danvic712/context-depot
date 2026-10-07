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

Install Docker. The `danvic712/context-depot` image includes the web interface and application runtime, so .NET and Bun do not need to be installed separately. Choose Docker Compose to start the application and database together, or run the application image against an existing database.

#### Option A: Docker Compose

Install Docker Compose and use [docker-compose.yaml](docker-compose.yaml) and [.env.example](.env.example) from the repository root. You can run them there or copy both files to a separate deployment directory.

Create your configuration:

```sh
cp .env.example .env
```

In `.env`, set `POSTGRES_PASSWORD` to a long, randomly generated password containing only letters and numbers. You can also adjust these options:

| Variable | Default | Purpose |
| --- | --- | --- |
| `CONTEXTDEPOT_VERSION` | `latest` | Application image tag |
| `HTTP_PORT` | `8080` | Server port for the web interface and MCP |
| `DATA_DIR` | `./data` | Parent directory for persistent data |

To access the service through your server's IP or domain, replace the `context-depot` service's `ports` entry in `docker-compose.yaml` with:

```yaml
ports:
  - "${HTTP_PORT:-8080}:8080"
```

This publishes the port on the server's network interfaces. Protect access to the web interface in your deployment. If you use a reverse proxy, configure the port mapping to match your setup.

Start the services and wait for the application to become healthy:

```sh
docker compose up -d --wait
```

Compose waits for PostgreSQL, runs `database-init` to enable pgvector, and then starts ContextDepot. The initialization service exits after completing its work. ContextDepot creates the required database tables on startup.

#### Option B: Run the image directly

Use this option if you already have PostgreSQL 17 with pgvector installed. Prepare a database and a user with permission to create the application's tables. In that database, enable the extension before starting ContextDepot:

```sql
CREATE EXTENSION IF NOT EXISTS vector;
```

Replace `<DB_HOST>`, `<DB_NAME>`, `<DB_USER>`, and `<DB_PASSWORD>` below with your database connection details. `<DB_HOST>` must be an IP or hostname reachable from the application container; adjust port `5432` if needed.

Run these commands on the server in your deployment directory:

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

The application creates its database tables on startup and stores documents, logs, and encryption keys in `data/context-depot`. Replace `latest` with a published release tag to pin the image version. To change the server port, change the first `8080` in `--publish 8080:8080`.

#### Access the service

Open `http://<SERVER_IP_OR_DOMAIN>:8080`. In every web and MCP address in this guide, replace `<SERVER_IP_OR_DOMAIN>` with your server's IP or domain, such as `192.168.1.100` or `context.example.com`. Use the port you published; if your reverse proxy provides HTTPS, use its HTTPS address instead.

### 2. Complete the first-run guide

1. **Create a space.** Choose a name and a path, such as `my-project`, and describe what belongs there.
2. **Configure optional model services.** Set up an Embedding model for semantic search, or skip Inference and configure it later.
3. **Create an MCP access key.** Give the key a recognizable name. You can also create keys later in Settings.
4. **Review and finish.** If you created a key, copy it along with the connection details. The full key is shown only once.

The guide saves all changes when you finish. Refreshing the page before then clears the draft.

### 3. Connect your agent

In a compatible MCP client, select **Streamable HTTP**, enter `http://<SERVER_IP_OR_DOMAIN>:8080/mcp` as the server address, and set the `X-ContextDepot-Key` request header to your access key.

For clients that use an `mcpServers` configuration, add:

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

Replace `<SERVER_IP_OR_DOMAIN>` with your server's IP or domain and `<YOUR_ACCESS_KEY>` with your key. Use the same service address and port as the web interface, including HTTPS if configured. Configuration formats vary by client; you can adapt the connection example provided by the setup guide.

In **Settings → Access keys**, grant the key access to each space the client should use. Add a grant whenever you create another space that the client needs to access.

### 4. Save and retrieve knowledge

Ask your connected agent to save or retrieve knowledge with requests like these. Replace `my-project` with your space path and provide the content you want to save:

> In the `my-project` space, remember this preference: review changes before merging and explain important tradeoffs.

> Save this project design as a Markdown document at `project-design.md` in the `my-project` space.

> Before continuing work on `my-project`, retrieve the relevant decisions, preferences, and design documents from ContextDepot.

Browse saved content in **Spaces**, or find context and documents with **Search**. Read the full content on the web, and use your connected agent to update or archive knowledge as the project changes.

## Data and updates

For Docker Compose deployments, both services store persistent data under `DATA_DIR`, which defaults to `./data` alongside the Compose file:

| Directory | Contents |
| --- | --- |
| `data/postgresql` | PostgreSQL database files |
| `data/context-depot` | Markdown documents, logs, and encryption keys |

The directories are created on startup. The direct `docker run` example uses `data/context-depot` for application data; back up its external PostgreSQL database separately. To make a consistent filesystem backup, stop the services before copying the entire data directory. Saved Inference credentials require the original encryption keys.

For a Compose deployment, stop the containers while keeping the data:

```sh
docker compose down
```

After backing up your data, pull the application image and recreate its container:

```sh
docker compose pull context-depot
docker compose up -d --wait context-depot
```

For a Compose deployment, pin an application version by setting `CONTEXTDEPOT_VERSION` in `.env` to a published release tag. Both deployment examples enable automatic container log rotation, with up to three files of 10 MB each per container.

For a container started with `docker run`, stop it with `docker stop context-depot` and view its logs with `docker logs context-depot`. To update it after backing up your data, pull the chosen image tag, remove the stopped container with `docker rm context-depot`, and repeat the `docker run` command using the same data directory.

If the application does not start in a Compose deployment, view its logs with `docker compose logs context-depot`.

## License

[MIT License](LICENSE)
