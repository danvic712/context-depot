import { beforeAll, describe, expect, test } from "bun:test";
import { renderToStaticMarkup } from "react-dom/server";
import { paginationPages, readPageSize } from "../../src/lib/pagination";
import { SpaceDirectoryFooter } from "../../src/features/spaces/SpaceDirectoryLayout";
import { initializeI18n } from "../../src/lib/i18n";

beforeAll(async () => {
  await initializeI18n("en", async () =>
    Object.assign(
      {},
      await Bun.file(
        new URL(
          "../../../../locales/en-US/workspace-browser.json",
          import.meta.url,
        ),
      ).json(),
      await Bun.file(
        new URL("../../../../locales/en-US/ui-states.json", import.meta.url),
      ).json(),
    ),
  );
});

describe("Space pagination", () => {
  test("page size accepts bounded custom values and rejects malformed input", () => {
    expect(readPageSize(null)).toBe(12);
    for (const value of ["6", "24", "48", "60"])
      expect(readPageSize(value)).toBe(Number(value));
    for (const value of ["0", "-1", "61", "1.5", "nope", ""])
      expect(readPageSize(value)).toBe(12);
  });
  test("small collections expose every page without gaps", () => {
    expect(paginationPages(1, 0)).toEqual([]);
    expect(paginationPages(2, 3)).toEqual([1, 2, 3]);
    expect(paginationPages(7, 7)).toEqual([1, 2, 3, 4, 5, 6, 7]);
  });
  test("large collections keep both ends and nearby pages in a bounded control", () => {
    expect(paginationPages(1, 100)).toEqual([1, 2, 3, 4, 5, "end-gap", 100]);
    expect(paginationPages(50, 100)).toEqual([
      1,
      "start-gap",
      49,
      50,
      51,
      "end-gap",
      100,
    ]);
    expect(paginationPages(100, 100)).toEqual([
      1,
      "start-gap",
      96,
      97,
      98,
      99,
      100,
    ]);
    expect(paginationPages(999, 10)).toEqual([1, "start-gap", 6, 7, 8, 9, 10]);
  });
  test("first and last pages expose the correct range and disabled boundaries", () => {
    const render = (page: number, itemCount: number) =>
      renderToStaticMarkup(
        <SpaceDirectoryFooter
          page={page}
          totalCount={29}
          itemCount={itemCount}
          onPageChange={() => {}}
        />,
      );
    const first = render(1, 12);
    expect(first).toContain("1–12 of 29 spaces");
    expect(first).toContain('aria-current="page"');
    expect(first).toMatch(/<button[^>]*disabled=""[^>]*>.*?Previous/s);
    const last = render(3, 5);
    expect(last).toContain("25–29 of 29 spaces");
    expect(last).not.toContain("25–36");
    expect(last).toMatch(/<button[^>]*disabled=""[^>]*aria-label="Next"/);
    expect(last).toContain('aria-label="Page 3"');
  });
  test("empty and single-page results do not expose unnecessary navigation", () => {
    const empty = renderToStaticMarkup(
      <SpaceDirectoryFooter totalCount={0} onPageChange={() => {}} />,
    );
    expect(empty).toContain("0 spaces");
    expect(empty).not.toContain("<nav");
    expect(empty).not.toContain("1–0");
    const single = renderToStaticMarkup(
      <SpaceDirectoryFooter
        totalCount={1}
        itemCount={1}
        onPageChange={() => {}}
      />,
    );
    expect(single).not.toContain("<nav");
  });
});
