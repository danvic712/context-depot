import { defaultUrlTransform } from "react-markdown";

// Allow web links and local paths; reject executable, data and custom protocols.
export function safeMarkdownUrl(url: string) {
  if (url.startsWith("//")) return "";
  if (/^[a-z][a-z\d+.-]*:/i.test(url) && !/^https?:/i.test(url)) return "";
  return defaultUrlTransform(url);
}
