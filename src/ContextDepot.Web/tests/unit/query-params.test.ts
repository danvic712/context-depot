import { describe, expect, test } from "bun:test";
import { normalizeKnowledgeParams } from "../../src/features/knowledge/query-params";

describe("knowledge URL filters", () => {
  test("invalid deep links fall back without losing the search term", () => {
    const result = normalizeKnowledgeParams(
      new URLSearchParams(
        "q=keep&type=unknown&workspace=missing&view=unknown&state=bad&preview=2",
      ),
    );
    expect(result.toString()).toBe("q=keep&workspace=missing");
  });
  test("document filters cannot retain a Context kind", () => {
    const result = normalizeKnowledgeParams(
      new URLSearchParams("type=documents&kind=decision&workspace=projects"),
    );
    expect(result.get("type")).toBe("documents");
    expect(result.has("kind")).toBe(false);
    expect(result.get("workspace")).toBe("projects");
  });
  test("a kind deep link selects Contexts and removes legacy simulation flags", () => {
    const result = normalizeKnowledgeParams(
      new URLSearchParams("q=copy&kind=preference&preview=1&state=degraded"),
    );
    expect(result.get("type")).toBe("contexts");
    expect(result.get("kind")).toBe("preference");
    expect(result.get("q")).toBe("copy");
    expect(result.has("preview")).toBe(false);
    expect(result.has("state")).toBe(false);
  });
  test("old sample states cannot override real search and content selection", () => {
    for (const state of [
      "loading",
      "empty",
      "error",
      "permission",
      "degraded",
    ]) {
      const result = normalizeKnowledgeParams(
        new URLSearchParams({
          q: "notes",
          workspace: "projects/research",
          type: "documents",
          selected: "document:resource",
          read: "1",
          preview: "1",
          state,
          view: "contexts",
        }),
      );
      expect(result.toString()).toBe(
        "q=notes&workspace=projects%2Fresearch&type=documents&selected=document%3Aresource&read=1",
      );
    }
  });
  test("accepts all seven Context kinds and full nested workspace paths", () => {
    for (const kind of [
      "fact",
      "preference",
      "decision",
      "goal",
      "state",
      "event",
      "observation",
    ]) {
      const params = new URLSearchParams({
        kind,
        workspace: " projects/research/search ",
        selected: "context:resource",
        read: "1",
      });
      const result = normalizeKnowledgeParams(params);
      expect(result.get("kind")).toBe(kind);
      expect(result.get("type")).toBe("contexts");
      expect(result.get("workspace")).toBe("projects/research/search");
      expect(result.get("selected")).toBe("context:resource");
      expect(result.get("read")).toBe("1");
    }
  });
  test("drops empty scopes and malformed selected resources", () => {
    const result = normalizeKnowledgeParams(
      new URLSearchParams("workspace=+&kind=0&selected=wrong:id&read=2"),
    );
    expect(result.size).toBe(0);
  });
});
