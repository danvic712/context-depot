import { stripDisplayParams } from "../../src/lib/display-query";
import { afterEach, beforeEach, describe, expect, test } from "bun:test";
import {
  readDisplayPreference,
  saveDisplayPreference,
} from "../../src/lib/display-preferences";
const descriptor = Object.getOwnPropertyDescriptor(globalThis, "localStorage");
let values: Map<string, string>;
function workingStorage() {
  Object.defineProperty(globalThis, "localStorage", {
    configurable: true,
    value: {
      getItem: (key: string) => values.get(key) ?? null,
      setItem: (key: string, value: string) => values.set(key, value),
    },
  });
}
beforeEach(() => {
  values = new Map();
  workingStorage();
});
afterEach(() => {
  workingStorage();
  saveDisplayPreference("knowledgeLayout", "list");
  if (descriptor) Object.defineProperty(globalThis, "localStorage", descriptor);
  else Reflect.deleteProperty(globalThis, "localStorage");
});
describe("Local display preferences", () => {
  test("uses bounded sizes and safe layout defaults for corrupted storage", () => {
    values.set("contextdepot.display.knowledgePageSize", "1000000");
    values.set("contextdepot.display.knowledgeLayout", "unknown");
    expect(readDisplayPreference("knowledgePageSize")).toBe(12);
    expect(readDisplayPreference("knowledgeLayout")).toBe("list");
    saveDisplayPreference("knowledgePageSize", 24);
    saveDisplayPreference("knowledgeLayout", "cards");
    expect(readDisplayPreference("knowledgePageSize")).toBe(24);
    expect(readDisplayPreference("knowledgeLayout")).toBe("cards");
    expect(values.get("contextdepot.display.knowledgePageSize")).toBe("24");
  });
  test("keeps the chosen display when writes are blocked but reads still work", () => {
    values.set("contextdepot.display.knowledgeLayout", "list");
    Object.defineProperty(globalThis, "localStorage", {
      configurable: true,
      value: {
        getItem: (key: string) => values.get(key) ?? null,
        setItem: () => {
          throw new Error("Storage quota exceeded");
        },
      },
    });
    saveDisplayPreference("knowledgeLayout", "cards");
    expect(readDisplayPreference("knowledgeLayout")).toBe("cards");
  });
  test("removes display options while retaining shareable filters and page positions", () => {
    const source = new URLSearchParams({
      pageSize: "24",
      knowledgeLayout: "cards",
      readerView: "source",
      archivePageSize: "6",
      knowledgePage: "2",
      q: "notes",
      directoryQuery: "q=design&page=3&pageSize=24",
    });
    expect(Object.fromEntries(stripDisplayParams(source))).toEqual({
      knowledgePage: "2",
      q: "notes",
      directoryQuery: "q=design&page=3",
    });
    expect(source.get("pageSize")).toBe("24");
  });
});
