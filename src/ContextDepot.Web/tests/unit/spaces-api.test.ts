import { describe, expect, test } from "bun:test";
import { httpClient } from "../../src/lib/http-client";
import { defaultDirectoryFilters } from "../../src/features/spaces/directory-query";
import {
  getSpace,
  getSpaceDirectory,
  parseDirectory,
  parseSpaceDetail,
} from "../../src/features/spaces/spaces-api";

import {
  WorkspaceContentType,
  WorkspaceUpdateWindow,
  WorkspaceSortOrder,
} from "../../src/features/spaces/directory-enums";

const space = {
  id: "root-id",
  name: "Research",
  description: null,
  path: "research",
  contextCount: 2,
  documentCount: 1,
  subspaceCount: 1,
  activityAt: "2026-10-02T00:00:00Z",
};
const directory = {
  asOf: space.activityAt,
  items: [space],
  totalCount: 13,
  page: 2,
  pageSize: 12,
};

describe("Space browser contracts", () => {
  test("keeps pagination and ancestors while rejecting malformed directory data", () => {
    expect(parseDirectory(directory).totalCount).toBe(13);
    expect(
      parseDirectory({ ...directory, items: [], totalCount: 0 }).items,
    ).toEqual([]);
    for (const invalid of [
      { totalCount: -1 },
      { page: 0 },
      { pageSize: 61 },
      { asOf: "invalid" },
      { items: [{ ...space, subspaceCount: -1 }] },
    ]) {
      expect(() => parseDirectory({ ...directory, ...invalid })).toThrow();
    }
    expect(
      parseSpaceDetail({
        workspace: { ...space, path: "research/child" },
        ancestors: [{ id: space.id, name: space.name, path: space.path }],
      }).ancestors[0]?.path,
    ).toBe("research");
    expect(() =>
      parseSpaceDetail({ workspace: space, ancestors: null }),
    ).toThrow();
  });

  test("requests paginated direct children and stable-ID details with cancellation", async () => {
    const requests: string[] = [];
    const server = Bun.serve({
      port: 0,
      fetch(request) {
        const url = new URL(request.url);
        requests.push(url.pathname + url.search);
        return Response.json(
          url.pathname.endsWith("/browse")
            ? {
                ...directory,
                pageSize: Number(url.searchParams.get("pageSize") ?? 12),
              }
            : { workspace: space, ancestors: [] },
        );
      },
    });
    const interceptor = httpClient.interceptors.request.use((config) => ({
      ...config,
      baseURL: `${server.url}api`,
    }));
    const controller = new AbortController();
    try {
      expect(
        (await getSpaceDirectory("root-id", 2, controller.signal)).page,
      ).toBe(2);
      expect((await getSpace(space.id, controller.signal)).workspace.name).toBe(
        "Research",
      );
      expect(requests).toEqual([
        "/api/workspaces/browse?parentId=root-id&page=2&pageSize=12",
        "/api/workspaces/root-id",
      ]);
      await getSpaceDirectory(undefined, 1, controller.signal, {
        ...defaultDirectoryFilters,
        query: "项目",
        content: WorkspaceContentType.Both,
        updated: WorkspaceUpdateWindow.Month,
        sort: WorkspaceSortOrder.Name,
      });
      const filtered = new URL(requests[2]!, server.url);
      expect(filtered.searchParams.get("query")).toBe("项目");
      expect(filtered.searchParams.get("content")).toBe("both");
      expect(filtered.searchParams.get("updatedWithinDays")).toBe("30");
      expect(filtered.searchParams.get("sort")).toBe("name");
      const custom = await getSpaceDirectory(
        undefined,
        2,
        controller.signal,
        defaultDirectoryFilters,
        6,
      );
      expect(custom.pageSize).toBe(6);
      expect(requests[3]).toBe("/api/workspaces/browse?page=2&pageSize=6");
      controller.abort();
      await expect(
        getSpaceDirectory(undefined, 1, controller.signal),
      ).rejects.toThrow();
      expect(requests.length).toBe(4);
    } finally {
      httpClient.interceptors.request.eject(interceptor);
      server.stop(true);
    }
  });
});
