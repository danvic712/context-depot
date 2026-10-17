import { expect, test } from "bun:test";
import { renderToStaticMarkup } from "react-dom/server";
import { MarkdownContent } from "../../src/components/content/MarkdownContent";

test("reader headings match the server validation fixtures, including Unicode and duplicate IDs", async () => {
  const fixtures = await Bun.file(
    new URL(
      "../../../../tests/fixtures/markdown-heading-links.json",
      import.meta.url,
    ),
  ).json();
  for (const fixture of fixtures) {
    const html = renderToStaticMarkup(
      <MarkdownContent content={fixture.content} />,
    );
    const ids = [
      ...html.matchAll(/<h[1-6] id="markdown-[^"]*?_-([^"]*)"/g),
    ].map((match) => match[1]);
    expect(ids, fixture.name).toEqual(fixture.headings);
  }
});
