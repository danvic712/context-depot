import { describe, expect, test } from "bun:test";
import { renderToStaticMarkup } from "react-dom/server";
import { MarkdownBody } from "../../src/features/knowledge/MarkdownBody";

describe("Markdown reader", () => {
  test("renders headings, lists, tables and fenced code", () => {
    const html = renderToStaticMarkup(
      <MarkdownBody
        content={
          "# Guide\n\n- First\n- Second\n\n| Name | Value |\n| --- | --- |\n| Scope | Local |\n\n```ts\nconst count = 1;\n```"
        }
      />,
    );
    expect(html).toContain("<h2>Guide</h2>");
    expect(html).toContain("<ul>");
    expect(html).toContain("<li>Second</li>");
    expect(html).toContain("<table>");
    expect(html).toContain("<td>Local</td>");
    expect(html).toContain('<pre><code class="language-ts">');
  });

  test("does not render raw HTML, executable links or external images", () => {
    const html = renderToStaticMarkup(
      <MarkdownBody
        content={
          "<script>alert(1)</script>\n\n[Unsafe](javascript:alert%281%29) [Data](data:text/html,test) [Protocol relative](//evil.example)\n\n![Remote image](https://example.com/image.png)"
        }
      />,
    );
    expect(html).not.toContain("<script");
    expect(html).not.toContain("href=");
    expect(html).not.toContain("<img");
    expect(html).toContain("Remote image");
  });

  test("keeps safe web and relative links readable", () => {
    const html = renderToStaticMarkup(
      <MarkdownBody content="[Docs](https://example.com/docs) [Local](/spaces)" />,
    );
    expect(html).toContain('href="https://example.com/docs"');
    expect(html).toContain('href="/spaces"');
  });
});
