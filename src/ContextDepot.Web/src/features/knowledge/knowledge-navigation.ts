export function knowledgeReturnTarget(state: unknown) {
  const fallback = { to: "/search", label: "knowledgeBackSearch" } as const;
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
  if (pathname === "/")
    return { to: from, label: "knowledgeBackHome" } as const;
  if (pathname === "/search")
    return { to: from, label: "knowledgeBackSearch" } as const;
  if (pathname === "/spaces" || /^\/spaces\/[^/]+$/.test(pathname ?? ""))
    return { to: from, label: "knowledgeBackSpaces" } as const;
  return fallback;
}
