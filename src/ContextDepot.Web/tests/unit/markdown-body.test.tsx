import { describe, expect, test } from "bun:test";
import { renderToStaticMarkup } from "react-dom/server";
import { MarkdownContent } from "../../src/components/content/MarkdownContent";

describe("Markdown reader", () => {
  test("renders headings, lists, tables and fenced code", () => {
    const html = renderToStaticMarkup(
      <MarkdownContent
        content={
          "# Guide\n\n- First\n- Second\n\n| Name | Value |\n| --- | --- |\n| Scope | Local |\n\n```ts\nconst count = 1;\n```"
        }
      />,
    );
    expect(html).toMatch(/<h1 id="markdown-[^"]+-guide">Guide<\/h1>/);
    expect(html).toContain("<ul>");
    expect(html).toContain("<li>Second</li>");
    expect(html).toContain("<table>");
    expect(html).toContain("<td>Local</td>");
    expect(html).toContain('<pre><code class="language-ts">');
  });

  test("does not render raw HTML, executable links or external images", () => {
    const html = renderToStaticMarkup(
      <MarkdownContent
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
      <MarkdownContent content="[Docs](https://example.com/docs) [Local](/spaces)" />,
    );
    expect(html).toContain('href="https://example.com/docs"');
    expect(html).toContain('href="/spaces"');
  });

  test("resolves encoded Chinese and duplicate heading links within each reader", () => {
    const content =
      "[详情](#%E8%AF%A6%E6%83%85) [Second](#details-1)\n\n## 详情\n\n## Details\n\n## Details\n\n### *Formatted* `heading`!";
    const html = renderToStaticMarkup(
      <>
        <MarkdownContent content={content} nested />
        <MarkdownContent content={content} nested />
      </>,
    );
    const ids = [...html.matchAll(/<h[2-6] id="([^"]+)"/g)].map(
      (match) => match[1]!,
    );
    expect(ids).toHaveLength(8);
    expect(new Set(ids).size).toBe(8);
    expect(ids[0]).toEndWith("-详情");
    expect(ids[2]).toEndWith("-details-1");
    expect(ids[3]).toEndWith("-formatted-heading");
    expect(html).toContain(`href="#${ids[0]}"`);
    expect(html).toContain(`href="#${ids[2]}"`);
    expect(html).toContain(`<h3 id="${ids[0]}">详情</h3>`);
  });

  test("isolates heading names from application IDs", () => {
    const html = renderToStaticMarkup(
      <MarkdownContent content="# location\n\n# __proto__\n\n# knowledge-search\n\n[Heading](#knowledge-search)" />,
    );
    expect(html).not.toContain('id="location"');
    expect(html).not.toContain('id="__proto__"');
    expect(html).not.toContain('id="knowledge-search"');
    expect(html).toMatch(/href="#markdown-[^"]+-knowledge-search"/);
  });
});
