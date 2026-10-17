import { defaultUrlTransform } from "react-markdown";

// Shared Markdown links allow web URLs and local paths, not executable protocols.
export function safeMarkdownUrl(url: string) {
  if (url.startsWith("//")) return "";
  if (/^[a-z][a-z\d+.-]*:/i.test(url) && !/^https?:/i.test(url)) return "";
  return defaultUrlTransform(url);
}

/** Local Markdown paths only; external URLs and application routes keep their normal behavior. */
export function markdownDocumentLink(href: string) {
  if (
    !href ||
    href.startsWith("#") ||
    href.startsWith("//") ||
    /^[a-z][a-z\d+.-]*:/i.test(href)
  )
    return;
  const hashIndex = href.indexOf("#");
  const beforeHash = hashIndex < 0 ? href : href.slice(0, hashIndex);
  const path = beforeHash.split("?", 1)[0]!;
  try {
    if (!decodeURIComponent(path).toLowerCase().endsWith(".md")) return;
  } catch {
    return;
  }
  return { path, hash: hashIndex < 0 ? "" : href.slice(hashIndex) };
}
