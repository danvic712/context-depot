export function knowledgeReturnTarget(
  state: unknown,
  spaceId?: string,
  search = "",
) {
  const fallback = spaceId
    ? {
        to: `/spaces/${encodeURIComponent(spaceId)}${search}`,
        label: "knowledgeBackSpaces" as const,
      }
    : { to: "/search", label: "knowledgeBackSearch" as const };
  if (!state || typeof state !== "object" || !("from" in state))
    return fallback;
  const from = state.from;
  if (
    typeof from !== "string" ||
    !from.startsWith("/") ||
    from.startsWith("//") ||
    from.includes("\\") ||
    Array.from(from).some((character) => character.charCodeAt(0) < 32)
  )
    return fallback;
  const pathname = from.split(/[?#]/, 1)[0];
  if (spaceId) {
    const spacePath = `/spaces/${encodeURIComponent(spaceId)}`;
    // Entering a space makes it the current reading context. Dismissing its
    // reader returns to that space, even when the entry point was Search.
    return pathname === spacePath || pathname === `${spacePath}/archived`
      ? { to: from, label: "knowledgeBackSpaces" as const }
      : fallback;
  }
  if (pathname === "/")
    return { to: from, label: "knowledgeBackHome" } as const;
  if (pathname === "/search")
    return { to: from, label: "knowledgeBackSearch" } as const;
  if (
    pathname === "/spaces" ||
    /^\/spaces\/[^/]+(?:\/archived)?$/.test(pathname ?? "")
  )
    return { to: from, label: "knowledgeBackSpaces" } as const;
  return fallback;
}
