import { describe, expect, test } from "bun:test";
import {
  defaultDirectoryFilters,
  directoryParams,
  directoryRequest,
  hasDirectoryFilters,
  readDirectoryFilters,
  workspaceDetailTarget,
  workspaceDirectoryParams,
} from "../../src/features/spaces/directory-query";

import {
  WorkspaceContentStatus,
  WorkspaceContentType,
  WorkspaceUpdateWindow,
  WorkspaceSortOrder,
} from "../../src/features/spaces/directory-enums";

describe("Workspace directory filters", () => {
  test("restores conditions while discarding unknown options and impossible dates", () => {
    expect(
      readDirectoryFilters(
        new URLSearchParams(
          "contentStatus=other&contents=other&updated=other&sort=other",
        ),
      ),
    ).toEqual(defaultDirectoryFilters);
    const filters = readDirectoryFilters(
      new URLSearchParams(
        "q=%20Project%20&contentStatus=empty&contents=both&updated=custom&from=2026-02-30&to=2026-10-12",
      ),
    );
    expect(filters.query).toBe("Project");
    expect(filters.content).toBe("all");
    expect(filters.from).toBe("");
    expect(filters.to).toBe("2026-10-12");
  });
  test("serializes combined conditions without stale dates or inactive options", () => {
    const filters = {
      ...defaultDirectoryFilters,
      query: "  项目  ",
      content: WorkspaceContentType.Documents,
      updated: WorkspaceUpdateWindow.Month,
      from: "2026-01-01",
      sort: WorkspaceSortOrder.Name,
    };
    const params = directoryParams(filters, 3);
    expect(params.get("q")).toBe("项目");
    expect(params.get("contents")).toBe("documents");
    expect(params.get("updated")).toBe("30");
    expect(params.get("from")).toBeNull();
    expect(params.get("page")).toBe("3");
    expect(params.has("layout")).toBe(false);
    expect(directoryRequest(readDirectoryFilters(params))).toMatchObject({
      query: "项目",
      content: "documents",
      updatedWithinDays: 30,
      updatedFrom: undefined,
      sort: "name",
    });
  });
  test("page size survives detail navigation and return without entering knowledge-page state", () => {
    const original = directoryParams(
      { ...defaultDirectoryFilters, query: "work" },
      2,
      6,
    );
    const target = new URL(
      workspaceDetailTarget("research", original),
      "https://example.com",
    );
    expect(workspaceDirectoryParams(target.searchParams, true).toString()).toBe(
      original.toString(),
    );
    expect(original.get("pageSize")).toBe("6");
    expect(
      directoryParams(defaultDirectoryFilters, 1, 12).has("pageSize"),
    ).toBe(false);
    const invalid = workspaceDirectoryParams(
      new URLSearchParams("pageSize=1000&page=2"),
    );
    expect(invalid.toString()).toBe("page=2");
  });
  test("date boundaries include the entire local end date", () => {
    const request = directoryRequest({
      ...defaultDirectoryFilters,
      updated: WorkspaceUpdateWindow.Custom,
      from: "2026-10-01",
      to: "2026-10-12",
    });
    expect(new Date(request.updatedFrom!).getDate()).toBe(1);
    expect(new Date(request.updatedFrom!).getHours()).toBe(0);
    expect(new Date(request.updatedBefore!).getDate()).toBe(13);
    expect(new Date(request.updatedBefore!).getHours()).toBe(0);
    expect(request.updatedWithinDays).toBeUndefined();
  });
  test("keeps filters, loaded pages and sorting when entering a space and returning", () => {
    const original = directoryParams(
      {
        ...defaultDirectoryFilters,
        query: "design",
        content: WorkspaceContentType.Both,
        sort: WorkspaceSortOrder.Knowledge,
      },
      3,
    );
    const target = new URL(
      workspaceDetailTarget("space-id", original),
      "https://example.com",
    );
    expect(target.searchParams.get("directoryPage")).toBe("3");
    expect(workspaceDirectoryParams(target.searchParams, true).toString()).toBe(
      original.toString(),
    );
    target.searchParams.set("directoryPage", "1");
    expect(
      workspaceDirectoryParams(target.searchParams, true).has("page"),
    ).toBe(false);
  });
  test("normalizes return links and keeps old page-only links compatible", () => {
    expect(
      workspaceDetailTarget("research", new URLSearchParams("page=3")),
    ).toBe("/spaces/research?directoryPage=3");
    const params = new URLSearchParams({
      directoryQuery:
        "q=notes&layout=cards&selected=document:secret&page=-1&redirect=https://example.com",
    });
    expect(workspaceDirectoryParams(params, true).toString()).toBe("q=notes");
    expect(hasDirectoryFilters(defaultDirectoryFilters)).toBe(false);
    expect(
      hasDirectoryFilters({
        ...defaultDirectoryFilters,
        sort: WorkspaceSortOrder.Name,
      }),
    ).toBe(false);
    expect(
      hasDirectoryFilters({
        ...defaultDirectoryFilters,
        status: WorkspaceContentStatus.Empty,
      }),
    ).toBe(true);
  });
});
