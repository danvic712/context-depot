import { describe, expect, test } from "bun:test";
import {
  filterKeyWorkspaces,
  setVisibleKeyGrants,
} from "../../src/features/settings/access-key-grants";

const spaces = [
  { id: "research", name: "阅读笔记", path: "reading-notes" },
  { id: "api", name: "API Design", path: "api-design" },
  { id: "photo", name: "摄影练习", path: "photography" },
];

describe("Access key space selection", () => {
  test("searches names and identifiers without changing the source order", () => {
    expect(
      filterKeyWorkspaces(spaces, " 阅读 ", false, []).map((space) => space.id),
    ).toEqual(["research"]);
    expect(
      filterKeyWorkspaces(spaces, "API-DESIGN", false, []).map(
        (space) => space.id,
      ),
    ).toEqual(["api"]);
    expect(filterKeyWorkspaces(spaces, "", false, [])).toEqual(spaces);
  });
  test("selected-only remains constrained by the search", () => {
    expect(filterKeyWorkspaces(spaces, "reading", true, ["api"])).toEqual([]);
    expect(
      filterKeyWorkspaces(spaces, "", true, ["photo", "research"]),
    ).toEqual([spaces[0], spaces[2]]);
  });
  test("bulk selection only grants displayed results and preserves grants outside the filter", () => {
    expect(
      setVisibleKeyGrants(["missing", "photo"], [spaces[1]], true),
    ).toEqual(["missing", "photo", "api"]);
    expect(setVisibleKeyGrants(["api"], [spaces[1]], true)).toEqual(["api"]);
    expect(
      setVisibleKeyGrants(["missing", "photo", "api"], [spaces[1]], false),
    ).toEqual(["missing", "photo"]);
  });
  test("empty results never change access", () => {
    expect(setVisibleKeyGrants(["missing", "photo"], [], true)).toEqual([
      "missing",
      "photo",
    ]);
    expect(setVisibleKeyGrants(["missing", "photo"], [], false)).toEqual([
      "missing",
      "photo",
    ]);
  });
});
