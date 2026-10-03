import { defaultUrlTransform } from "react-markdown";

// Shared Markdown links allow web URLs and local paths, not executable protocols.
export function safeMarkdownUrl(url: string) {
  if (url.startsWith("//")) return "";
  if (/^[a-z][a-z\d+.-]*:/i.test(url) && !/^https?:/i.test(url)) return "";
  return defaultUrlTransform(url);
}
