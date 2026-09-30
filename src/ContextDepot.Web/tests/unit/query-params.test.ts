import { describe, expect, test } from "bun:test";
import { normalizeKnowledgeParams } from "../../src/features/knowledge/query-params";

describe("knowledge URL filters", () => {
  test("invalid deep links fall back without losing the search term", () => {
    const result = normalizeKnowledgeParams(
      new URLSearchParams(
        "q=keep&type=unknown&workspace=missing&view=unknown&state=bad&preview=2",
      ),
    );
    expect(result.toString()).toBe("q=keep");
  });
  test("document filters cannot retain a Context kind", () => {
    const result = normalizeKnowledgeParams(
      new URLSearchParams("type=documents&kind=decision&workspace=projects"),
    );
    expect(result.get("type")).toBe("documents");
    expect(result.has("kind")).toBe(false);
    expect(result.get("workspace")).toBe("projects");
  });
  test("a kind deep link selects Contexts and preserves preview", () => {
    const result = normalizeKnowledgeParams(
      new URLSearchParams("q=copy&kind=preference&preview=1&state=degraded"),
    );
    expect(result.get("type")).toBe("contexts");
    expect(result.get("kind")).toBe("preference");
    expect(result.get("q")).toBe("copy");
    expect(result.get("state")).toBe("degraded");
  });
});
