import { expect, test, spyOn } from "bun:test";
import { httpClient } from "../../src/lib/http-client";
import {
  createAccessKey,
  updateAccessKeyGrants,
  parseAccessKey,
} from "../../src/features/settings/settings-api";

const key = {
  id: "key",
  name: "Client",
  prefix: "cdk_example",
  createdAt: "2026-10-09T00:00:00Z",
  lastUsedAt: null,
  revokedAt: null,
  workspaceIds: [],
  canCreateRootWorkspaces: true,
  accessVersion: "9007199254740993",
};

test("access versions retain decimal precision and invalid permissions fail closed", () => {
  expect(parseAccessKey(key).accessVersion).toBe("9007199254740993");
  expect(
    parseAccessKey({ ...key, accessVersion: "9223372036854775807" })
      .accessVersion,
  ).toBe("9223372036854775807");
  for (const accessVersion of [1, "-1", "1.5", "", "00", "9223372036854775808"])
    expect(() => parseAccessKey({ ...key, accessVersion })).toThrow();
  for (const canCreateRootWorkspaces of [undefined, "true", 1])
    expect(() => parseAccessKey({ ...key, canCreateRootWorkspaces })).toThrow();
});

test("creation from zero grants explicitly sends capability through the management boundary", async () => {
  const request = spyOn(httpClient, "request").mockResolvedValue({
    data: { key, secret: "cdk_example." + "x".repeat(32) },
  });
  try {
    const signal = new AbortController().signal;
    const result = await createAccessKey("Client", [], signal, true);
    expect(result.key.canCreateRootWorkspaces).toBe(true);
    expect(request).toHaveBeenCalledWith(
      expect.objectContaining({
        method: "POST",
        signal,
        headers: { "X-ContextDepot-Management": "web" },
        data: {
          name: "Client",
          workspaceIds: [],
          canCreateRootWorkspaces: true,
        },
      }),
    );
  } finally {
    request.mockRestore();
  }
});

test("access editing sends capability grants and the exact version in a single request", async () => {
  const request = spyOn(httpClient, "request").mockResolvedValue({ data: key });
  try {
    const signal = new AbortController().signal;
    await updateAccessKeyGrants(key.id, [], signal, true, key.accessVersion);
    expect(request).toHaveBeenCalledWith(
      expect.objectContaining({
        method: "PUT",
        signal,
        data: {
          workspaceIds: [],
          canCreateRootWorkspaces: true,
          expectedAccessVersion: "9007199254740993",
        },
      }),
    );
  } finally {
    request.mockRestore();
  }
});
