# ContextDepot

[English](README.md) · [简体中文](README.zh-CN.md)

ContextDepot gives your AI assistants a shared place to keep knowledge. Save facts, preferences, decisions and Markdown documents, then reuse them in another task or conversation. Connect an assistant through MCP (Model Context Protocol); use the web interface to browse, search and manage what you keep.

This guide walks you through deployment, connecting an assistant, and saving your first piece of knowledge. Codex can also submit conversations for optional background memory capture.

## Before you start

- **To deploy:** Docker and Docker Compose. The application image includes the web interface and runtime; no separate .NET or Bun installation is needed.
- **To connect an assistant:** a client that supports MCP over Streamable HTTP.
- **To search:** keyword search works without a model. Add a search (Embedding) model under **Settings → Model services** to search by meaning.

A **space** groups knowledge by project or topic; MCP tools call it a **Workspace**. Save short facts or decisions as **context**, and longer notes as **Markdown documents**. An **access key** controls which spaces a connected app can use.

## 1. Start ContextDepot

### Recommended: Docker Compose

Use [docker-compose.yaml](docker-compose.yaml) and [.env.example](.env.example) from the repository root, or copy both into a deployment directory. In that directory, create your configuration:

```sh
cp .env.example .env
```

Edit `.env` and set `POSTGRES_PASSWORD` to a long, randomly generated password containing only letters and numbers. Adjust these optional values if needed:

| Variable | Default | Purpose |
| --- | --- | --- |
| `CONTEXTDEPOT_VERSION` | `latest` | Application image tag; use a published release tag to pin a version |
| `HTTP_PORT` | `8080` | Web interface and MCP port |
| `DATA_DIR` | `./data` | Persistent data directory |

Start the services:

```sh
docker compose up -d --wait
```

Compose starts PostgreSQL 17, enables pgvector, and starts ContextDepot. The `database-init` container exits once initialization is complete; this is expected. ContextDepot creates its database tables on startup.

Open **http://localhost:8080** on the deployment machine, or use your configured `HTTP_PORT`.

**Connecting from another device?** Compose binds the port to loopback by default. Use an HTTPS reverse proxy, or change the application's port mapping to `"${HTTP_PORT:-8080}:8080"` to expose it on the server's network interfaces. Protect access to the web interface; MCP access keys do not replace deployment-level protection. Use the same reachable base address for the web interface and `/mcp`.

### Alternative: use an existing PostgreSQL database

Prepare PostgreSQL 17 with pgvector and a database user allowed to create the application's tables. Enable the extension in that database:

```sql
CREATE EXTENSION IF NOT EXISTS vector;
```

Replace the database placeholders below. The database host must be reachable from the container; change port `5432` if needed. Run this from your deployment directory:

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

This command exposes port `8080` on the server's network interfaces. Apply the same access protection described above. Change the first port in `--publish 8080:8080` if needed, and replace `latest` with a published release tag to pin a version.

## 2. Complete the first-run guide

1. **Create a space.** Choose a recognizable name and identifier, such as `my-project`. Describe what you plan to keep there.
2. **Set up search, or skip it.** A search model enables semantic search. You can add one later under **Settings → Model services**; keyword search is available without it.
3. **Create an access key.** Name it after the app you want to connect. You can also create keys later under **Settings → Access keys**.
4. **Review and save.** Copy the full access key and connection details when they appear. The full key is shown only once.

The guide saves all settings at the last step. Keep the page open without refreshing until you finish.

## 3. Connect your assistant

In your client's MCP settings, select **Streamable HTTP** and enter:

- **Server URL:** your ContextDepot address followed by `/mcp`, for example `https://context-depot.example.com/mcp`.
- **Request header:** `X-ContextDepot-Key`, with your access key as its value. ContextDepot does not use Bearer authorization.

Use `http://localhost:8080/mcp` only when the client runs on the deployment machine. For a remote client, use an address it can reach and HTTPS for network connections.

For clients using an `mcpServers` configuration:

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

Replace the URL and `<YOUR_ACCESS_KEY>`. Client formats differ; adapt the example to your client and keep credentials out of version control.

In **Settings → Access keys**, grant this key access to your space. Allowing an app to create spaces automatically grants access only to spaces created with that key—not to existing spaces or spaces created elsewhere.

### Optional: install the ContextDepot Skill

The [Skill](skills/context-depot/SKILL.md) guides compatible assistants through retrieval and saving. Configure MCP first, then install with Node.js and [the skills CLI](https://github.com/vercel-labs/skills):

```sh
npx skills add danvic712/context-depot --skill context-depot --global
```

Select only the assistant you are using. `--global` makes the Skill available across its projects. From a local checkout, replace `danvic712/context-depot` with `./skills/context-depot`.

Installing the Skill does not grant space access or enable automatic capture. You can use the requests below as soon as your assistant can access the MCP tools.

## 4. Save your first piece of knowledge

Send this to your connected assistant, replacing `my-project` with your space identifier:

> In the `my-project` space, remember this preference: review changes before merging and explain important tradeoffs.

Open **Spaces** in ContextDepot and check that the preference appears in the intended space. Then try a new conversation:

> Before continuing work on `my-project`, retrieve the relevant preferences and decisions from ContextDepot.

For longer content, provide the text and ask:

> Save this project design as a Markdown document at `project-design.md` in the `my-project` space.

Use **Search** to find saved knowledge. The web interface lets you browse and read it; ask your connected assistant to update or archive it as your needs change.

## Optional: enable Codex session memory

Session memory processes submitted conversations in the background and saves useful, user-supported knowledge. It is **off by default** and currently supports Codex. Explicit saving and ordinary retrieval do not require it.

**Automatic space selection is a preview.** Live-model quality evaluation and installed-Codex end-to-end acceptance are still pending. Use it only if you accept those limitations.

### Set up capture

1. In **Settings → Model services**, configure a **Chat model**. Also configure a search model for vector-based space candidate lookup.
2. In **Settings → Access keys**, grant the Codex key access to the existing spaces you want it to use. Give those spaces clear names and descriptions. Automatic capture does not create spaces or grant access.
3. Open **Settings → Session memory** and enable it. This application-wide switch allows capture; you must also configure Codex to submit conversations.
4. Use a Codex version that supports native [`mcp_tool` Hooks](https://developers.openai.com/codex/hooks/#configure-an-mcp-tool-hook). In `~/.codex/config.toml`, configure or reuse the MCP connection:

   ```toml
   [mcp_servers.context-depot]
   url = "https://context-depot.example.com/mcp"
   env_http_headers = { "X-ContextDepot-Key" = "CONTEXTDEPOT_MCP_TOKEN" }
   ```

   Replace the URL. Supply `CONTEXTDEPOT_MCP_TOKEN` through your normal secure environment setup so Codex can read it. Keep the key out of Hook files and version control.
5. Merge the `UserPromptSubmit` and `Stop` entries from [hooks.example.json](scripts/codex/hooks.example.json) into `~/.codex/hooks.json` or a trusted project's `.codex/hooks.json`. Both `server` values must match the MCP connection name (`context-depot` here). Review and trust the definitions using Codex `/hooks`, and confirm MCP is connected.
6. Start a **new conversation**. If you used the old command adapter, remove its Hook registrations first; do not switch capture methods mid-turn. When Hooks own capture, disable Skill-driven capture of the same turns. Skill retrieval and explicit saves can remain enabled.

These Hooks call MCP directly: **no Python, JavaScript or shell runtime is required**. They submit the visible prompt and assistant reply with native session/turn IDs, not hidden reasoning, tool output or transcript files.

### Check the result

Complete a conversation containing a clear fact or decision you want retained. If you have a returned `receiptId` or `turnReceiptId`, ask your assistant to query `session_capture_status` with exactly one of them. `accepted` means queued, **not saved**. If the result is `saved`, use `context_get` with the returned Context IDs to inspect the space, summary and supporting user quotes. You can also browse or search the destination space in the web interface.

Not every conversation becomes a memory: the service may find no lasting knowledge or no clear authorized space. An assistant's explanation alone is not evidence for a saved fact.

### What to expect

- **Privacy:** minimal verbatim user quotes remain with saved knowledge long-term, even after temporary conversation bodies expire. Enable capture only with consent for that retention. Sensitive-data detection cannot guarantee that all sensitive content is excluded.
- **Connection limits:** Hooks require an already-connected MCP server. They add no reconnect, retry or offline replay; a failed call can lose a message. The example uses a five-second active execution timeout and does not guarantee exactly-once delivery.
- **Repeated replies:** repeated Stop events are deduplicated. The first accepted assistant body is kept; a changed later body conflicts rather than replacing it. Do not assume the final revision was captured.
- **Turning it off:** new sessions and captures stop, and unclaimed work pauses. Work already claimed may finish. Saved memories remain; retention deadlines still run. Re-enabling resumes only unexpired work.
- **Existing deployments:** enable capture in the UI even if you previously used `AgentSessions__Enabled`; that environment variable no longer controls it. The current instance applies this switch without restarting. Scheduled configuration refreshes run every minute; run only one application instance.

For managed session lifecycle, recall and status tools instead of native Hooks, see the [Skill's Session Memory instructions](skills/context-depot/SKILL.md#session-memory). Use one capture workflow per turn.

## Background processing

Run **one application instance**. Session capture, index repair, application settings refresh and inference refresh use Hangfire.InMemory in the same process, once per minute. A category skips overlapping invocations rather than running concurrently with itself. Capture throughput is bounded by the existing per-cycle limits; changes may take multiple minute cycles to reach all background tasks.

Hangfire definitions, queued jobs and execution history are held only in memory. Restarting clears them and registers the four schedules again; the next cycle scans the existing business queues in PostgreSQL. Interrupted business work may wait for its lease to expire. Missed minutes are not replayed. PostgreSQL remains required for business data, but no Hangfire schema or additional database permissions are needed. There is no Hangfire Dashboard.

When upgrading, stop the old application process before starting the new one. The startup migration deletes the obsolete `IndexRepair:PollIntervalSeconds` database setting. The application ignores `ContextDepot:IndexRepair:PollIntervalSeconds` in JSON and `ContextDepot__IndexRepair__PollIntervalSeconds` in the environment; neither affects scheduling or needs to be removed before startup. The old `AgentSessions:PollIntervalSeconds` / `AgentSessions__PollIntervalSeconds` settings are also ignored. These obsolete entries can be removed from deployment configuration. Other index setting names remain strictly checked, and batch, timeout and business retry limits remain unchanged.

Before rolling back to a version that requires the old database setting, stop the new process and reverse the cleanup migration before starting the old version. The downgrade restores the legacy default interval of 30 seconds without overwriting an existing setting; it does not recover a previously customized value. Retain all business data. Hangfire's in-memory state does not need a backup or migration.

## Back up and update

Compose stores persistent data under `DATA_DIR` (default: `./data`):

| Directory | Contents |
| --- | --- |
| `data/postgresql` | PostgreSQL database files |
| `data/context-depot` | Markdown documents, logs and encryption keys |

For a consistent filesystem backup, stop the services before copying the whole data directory:

```sh
docker compose down
# Back up your data directory before continuing.
docker compose pull context-depot
docker compose up -d --wait context-depot
```

Keep the encryption keys: saved model-service credentials require the original keys. Set `CONTEXTDEPOT_VERSION` in `.env` to a published release tag if you want a pinned version. Both deployment examples rotate container logs, keeping up to three 10 MB files per container.

For `docker run` deployments, back up the external database separately along with `data/context-depot`. Stop the application with `docker stop context-depot`, pull your chosen image, remove the stopped container with `docker rm context-depot`, and repeat the original run command using the same data directory.

## Troubleshooting

- **The application does not start:** run `docker compose logs context-depot database-init` (or `docker logs context-depot` for a standalone container).
- **A client cannot connect:** check that the URL is reachable from the client, ends in `/mcp`, and uses the `X-ContextDepot-Key` header with an active key.
- **A space is unavailable:** check the key's exact space grants under **Settings → Access keys**. Access to a parent does not automatically grant access to its children.
- **Search by meaning is unavailable:** check the search model under **Settings → Model services**. Keyword search remains available.
- **No memory appeared:** check both the server switch and Codex Hooks, the Chat model, the MCP connection and space grants. An accepted receipt alone does not confirm a save.

## License

[MIT License](LICENSE)
