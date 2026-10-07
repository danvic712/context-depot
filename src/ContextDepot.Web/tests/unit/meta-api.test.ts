import { describe, expect, test } from "bun:test";
import {
  getApplicationMeta,
  parseApplicationMeta,
} from "../../src/lib/meta-api";
import { httpClient, isRequestCanceled } from "../../src/lib/http-client";

describe("Application metadata API", () => {
  test("preserves the backend version and permits explicitly missing metadata", () => {
    expect(parseApplicationMeta({ version: "1.2.3-rc.1" })).toEqual({
      version: "1.2.3-rc.1",
    });
    expect(parseApplicationMeta({ version: null })).toEqual({ version: null });
    for (const value of [
      null,
      [],
      "1.2.3",
      {},
      { version: 1 },
      { version: "" },
      { version: " " },
    ])
      expect(() => parseApplicationMeta(value)).toThrow();
  });
  test("uses the independent metadata endpoint and forwards cancellation", async () => {
    const paths: string[] = [];
    const server = Bun.serve({
      hostname: "127.0.0.1",
      port: 0,
      fetch(request) {
        paths.push(new URL(request.url).pathname);
        expect(request.headers.has("X-ContextDepot-Management")).toBe(false);
        return Response.json({ version: "1.2.3-beta.2" });
      },
    });
    const baseURL = httpClient.defaults.baseURL;
    httpClient.defaults.baseURL = `${server.url.origin}/api`;
    try {
      expect(await getApplicationMeta(new AbortController().signal)).toEqual({
        version: "1.2.3-beta.2",
      });
      expect(paths).toEqual(["/api/meta"]);
      const controller = new AbortController();
      controller.abort();
      const error = await getApplicationMeta(controller.signal).catch(
        (error) => error,
      );
      expect(isRequestCanceled(error)).toBe(true);
      expect(paths).toHaveLength(1);
    } finally {
      httpClient.defaults.baseURL = baseURL;
      server.stop(true);
    }
  });
});
