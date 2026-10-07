import { afterEach, beforeAll, describe, expect, test } from "bun:test";
import {
  chmodSync,
  mkdtempSync,
  readFileSync,
  rmSync,
  writeFileSync,
} from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";

const script = new URL(
  "../../artifacts/release-tool/release.dll",
  import.meta.url,
).pathname;
beforeAll(() => {
  if (!Bun.file(script).size)
    throw new Error(
      "Build release tooling first: dotnet build scripts/release.cs -o artifacts/release-tool",
    );
});
const revision = "a".repeat(40);
const digest = `sha256:${"b".repeat(64)}`;
const images = ["example/context-depot", "ghcr.io/example/context-depot"];
const directories: string[] = [];
afterEach(() => {
  for (const path of directories.splice(0))
    rmSync(path, { recursive: true, force: true });
});

function manifest(version = "1.2.3", sha = revision, hash = digest) {
  return {
    digest: hash,
    annotations: {
      "org.opencontainers.image.version": version,
      "org.opencontainers.image.revision": sha,
    },
    manifests: ["amd64", "arm64"].map((architecture) => ({
      platform: { os: "linux", architecture },
    })),
  };
}
type RegistryManifest = ReturnType<typeof manifest>;
function fixture(tag = "v1.2.3", prerelease = false) {
  const directory = mkdtempSync(join(tmpdir(), "context-depot-release-test-"));
  directories.push(directory);
  const event = {
    release: { id: 42, tag_name: tag, prerelease, draft: false },
  };
  const state = {
    refs: {} as Record<string, RegistryManifest>,
    latest: event.release,
    latestSequence: [] as (typeof event.release)[],
    httpStatus: "200",
    registryError: "",
    calls: 0,
  };
  writeFileSync(join(directory, "event.json"), JSON.stringify(event));
  const fake = `#!/usr/bin/env bash
set -euo pipefail
command_name=$(basename "$0")
printf '%s %s\\n' "$command_name" "$*" >> "$MOCK_LOG"
case "$command_name" in
  git) printf '%s\\n' "$MOCK_REVISION" ;;
  docker)
    if [[ $3 == inspect ]]; then
      error=$(jq -r '.registryError' "$MOCK_STATE")
      [[ -z $error ]] || { printf '%s\\n' "$error" >&2; exit 1; }
      value=$(jq -c --arg ref "$4" '.refs[$ref] // null' "$MOCK_STATE")
      [[ $value != null ]] || { printf 'ERROR: manifest unknown\\n' >&2; exit 1; }
      printf '%s' "$value"
    elif [[ $3 != create ]]; then exit 2; fi ;;
  skopeo)
    [[ $1 == copy && $2 == --all && $3 == --preserve-digests ]] || exit 2
    source_ref=\${6#docker://}
    target_ref=\${7#docker://}
    jq --arg source "$source_ref" --arg target "$target_ref" '.refs[$target] = .refs[$source]' "$MOCK_STATE" > "$MOCK_STATE.next"
    mv "$MOCK_STATE.next" "$MOCK_STATE" ;;
  *) exit 2 ;;
esac
`;
  for (const name of ["git", "docker", "skopeo"]) {
    const path = join(directory, name);
    writeFileSync(path, fake);
    chmodSync(path, 0o755);
  }
  const add = (
    image: string,
    imageTag: string,
    value = manifest(tag.replace(/^v/, "")),
  ) => {
    state.refs[`${image}:${imageTag}`] = value;
    state.refs[`${image}@${value.digest}`] = value;
  };
  return {
    state,
    add,
    async run(operation: string, overrides: Record<string, string> = {}) {
      writeFileSync(join(directory, "state.json"), JSON.stringify(state));
      writeFileSync(join(directory, "output"), "");
      writeFileSync(join(directory, "log"), "");
      const server = Bun.serve({
        hostname: "127.0.0.1",
        port: 0,
        fetch() {
          const saved = JSON.parse(
            readFileSync(join(directory, "state.json"), "utf8"),
          ) as typeof state;
          const release = saved.latestSequence[saved.calls] ?? saved.latest;
          saved.calls += 1;
          writeFileSync(join(directory, "state.json"), JSON.stringify(saved));
          return Response.json(release, {
            status: Number(saved.httpStatus) || 503,
          });
        },
      });
      const process = Bun.spawn(["dotnet", script, operation], {
        stdout: "pipe",
        stderr: "pipe",
        env: {
          ...globalThis.process.env,
          PATH: `${directory}:${globalThis.process.env.PATH}`,
          MOCK_STATE: join(directory, "state.json"),
          MOCK_LOG: join(directory, "log"),
          MOCK_REVISION: revision,
          GITHUB_SHA: revision,
          GITHUB_EVENT_PATH: join(directory, "event.json"),
          GITHUB_OUTPUT: join(directory, "output"),
          GITHUB_REPOSITORY: "example/context-depot",
          GITHUB_API_URL:
            state.httpStatus === "network"
              ? "http://127.0.0.1:1"
              : server.url.origin,
          GITHUB_TOKEN: "fixture-placeholder",
          DOCKERHUB_IMAGE: images[0],
          GHCR_IMAGE: images[1],
          DOCKERHUB_DIGEST: digest,
          GHCR_DIGEST: digest,
          ...overrides,
        },
      });
      try {
        const [status, stdout, stderr] = await Promise.all([
          process.exited,
          new Response(process.stdout).text(),
          new Response(process.stderr).text(),
        ]);
        return {
          status,
          stdout,
          stderr,
          output: readFileSync(join(directory, "output"), "utf8"),
          log: readFileSync(join(directory, "log"), "utf8"),
          state: JSON.parse(
            readFileSync(join(directory, "state.json"), "utf8"),
          ) as typeof state,
        };
      } finally {
        server.stop(true);
      }
    },
  };
}

describe("C# file-based release workflow", () => {
  test("normalizes optional v and supports every published channel", async () => {
    for (const prefix of ["", "v"])
      for (const channel of ["stable", "alpha", "beta", "rc", "preview"]) {
        const version = channel === "stable" ? "1.2.3" : `1.2.3-${channel}.1`;
        const result = await fixture(
          `${prefix}${version}`,
          channel !== "stable",
        ).run("resolve");
        expect(result.status).toBe(0);
        expect(result.output).toContain(
          `app_version=${version}\nchannel=${channel}\n`,
        );
      }
  }, 30000);
  test("rejects invalid versions, prerelease flag conflicts and a moved tag", async () => {
    for (const tag of [
      "v1.2.3-dev",
      "v01.2.3",
      "v1.2.3-rc.0",
      "v1.2.3-rc.01",
      "v1.2.3+sha",
      "v65535.0.0",
      "vv1.2.3",
      "v1.2.3\n",
    ])
      expect((await fixture(tag).run("resolve")).status).not.toBe(0);
    expect((await fixture("v1.2.3", true).run("resolve")).status).not.toBe(0);
    expect(
      (await fixture("v1.2.3-rc.1", false).run("resolve")).status,
    ).not.toBe(0);
    expect(
      (await fixture().run("resolve", { GITHUB_SHA: "c".repeat(40) })).status,
    ).not.toBe(0);
  });
  test("a new version builds; an existing identical version is reused", async () => {
    const f = fixture();
    expect((await f.run("prepare")).output).toBe("build_required=true\n");
    for (const image of images) f.add(image, "v1.2.3");
    const result = await f.run("prepare");
    expect(result.status).toBe(0);
    expect(result.output).toBe("build_required=false\n");
    expect(result.log).not.toContain("skopeo copy");
  });
  test("repairs partial publication without rebuilding or dropping platforms", async () => {
    const f = fixture();
    f.add(images[0], "v1.2.3");
    const result = await f.run("prepare");
    expect(result.status).toBe(0);
    expect(result.output).toBe("build_required=false\n");
    expect(result.log).toContain("skopeo copy --all --preserve-digests");
    expect(result.state.refs[`${images[1]}:v1.2.3`].digest).toBe(digest);
  });
  test("checks the alternate v alias before publishing", async () => {
    const f = fixture();
    f.add(images[0], "1.2.3", manifest("1.2.3", "c".repeat(40)));
    const result = await f.run("prepare");
    expect(result.status).not.toBe(0);
    expect(result.log).not.toContain("skopeo copy");
    expect(result.output).toBe("");
  });
  test("rejects conflicting digests, missing architectures and registry authorization errors", async () => {
    const f = fixture();
    f.add(images[0], "v1.2.3");
    f.add(
      images[1],
      "v1.2.3",
      manifest("1.2.3", revision, `sha256:${"c".repeat(64)}`),
    );
    expect((await f.run("prepare")).status).not.toBe(0);
    f.add(images[1], "v1.2.3", {
      ...manifest(),
      manifests: [{ platform: { os: "linux", architecture: "amd64" } }],
    });
    expect((await f.run("record")).status).not.toBe(0);
    f.state.registryError = "denied: manifest not found";
    expect((await f.run("prepare")).status).not.toBe(0);
  });
  test("records verified identical registry digests", async () => {
    const f = fixture();
    for (const image of images) f.add(image, "v1.2.3");
    const result = await f.run("record");
    expect(result.status).toBe(0);
    expect(result.output).toBe(
      `dockerhub_digest=${digest}\nghcr_digest=${digest}\n`,
    );
  });
  test("only the current stable release promotes both verified digests", async () => {
    const f = fixture();
    for (const image of images) f.add(image, "v1.2.3");
    const result = await f.run("promote");
    expect(result.status).toBe(0);
    for (const image of images)
      expect(result.log).toContain(
        `imagetools create --tag ${image}:latest ${image}@${digest}`,
      );
    expect(result.state.calls).toBe(2);
  });
  test("prereleases and old stable reruns never update latest", async () => {
    expect(
      (await fixture("v1.2.3-rc.1", true).run("promote")).log,
    ).not.toContain("imagetools create");
    const f = fixture();
    for (const image of images) f.add(image, "v1.2.3");
    f.state.latest.id = 43;
    const result = await f.run("promote");
    expect(result.status).toBe(0);
    expect(result.log).not.toContain("imagetools create");
  });
  test("checks latest before each write and fails on lookup errors", async () => {
    const f = fixture();
    for (const image of images) f.add(image, "v1.2.3");
    f.state.latestSequence = [f.state.latest, { ...f.state.latest, id: 43 }];
    const result = await f.run("promote");
    expect(result.status).toBe(0);
    expect(result.log.match(/imagetools create/g)?.length).toBe(1);
    for (const status of ["403", "network"]) {
      f.state.httpStatus = status;
      const failure = await f.run("promote");
      expect(failure.status).not.toBe(0);
      expect(failure.log).not.toContain("imagetools create");
    }
  });
});
