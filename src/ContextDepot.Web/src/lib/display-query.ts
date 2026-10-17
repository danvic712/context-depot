/** Keeps browser-only display choices out of shareable application URLs. */
export function stripDisplayParams(params: URLSearchParams): URLSearchParams {
  const next = new URLSearchParams(params);
  for (const key of [
    "pageSize",
    "pagesize",
    "knowledgePageSize",
    "archivePageSize",
    "knowledgeLayout",
    "readerView",
    "readerPresentation",
  ])
    next.delete(key);
  if (next.has("directoryQuery")) {
    const original = next.get("directoryQuery")!;
    const nested = new URLSearchParams(original);
    nested.delete("pageSize");
    nested.delete("pagesize");
    if (nested.toString() !== original) {
      if (nested.size) next.set("directoryQuery", nested.toString());
      else next.delete("directoryQuery");
    }
  }
  return next;
}
