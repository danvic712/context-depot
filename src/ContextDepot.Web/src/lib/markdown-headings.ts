import type { Nodes, Root } from "hast";

/** Matches the server's Rune/UnicodeCategory heading rules. */
export function markdownHeadingSlug(text: string) {
  return text
    .toLowerCase()
    .replace(/[^\p{L}\p{M}\p{Nd}\p{Nl}\p{Pc}\u24B6-\u24E9 -]/gu, "")
    .replace(/ /g, "-");
}

function headingText(node: Nodes): string {
  if (node.type === "text") return node.value;
  return "children" in node ? node.children.map(headingText).join("") : "";
}

export function rehypeMarkdownHeadings({
  prefix = "",
}: { prefix?: string } = {}) {
  return (tree: Root) => {
    const ids = new Set<string>();
    function visit(node: Nodes) {
      if (
        node.type === "element" &&
        /^h[1-6]$/.test(node.tagName) &&
        !node.properties.id
      ) {
        const original = markdownHeadingSlug(headingText(node));
        let id = original;
        for (let suffix = 1; ids.has(id); suffix++)
          id = `${original}-${suffix}`;
        ids.add(id);
        node.properties.id = prefix + id;
      }
      if ("children" in node) node.children.forEach(visit);
    }
    visit(tree);
  };
}
