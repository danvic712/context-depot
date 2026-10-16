/** Shape checks shared by HTTP adapters; domain-specific validation stays with each contract. */
export function responseRecord(value: unknown): Record<string, unknown> {
  if (!value || typeof value !== "object" || Array.isArray(value))
    throw new Error("Invalid API object");
  return value as Record<string, unknown>;
}

export function responseText(value: unknown): string {
  if (typeof value !== "string") throw new Error("Invalid API text");
  return value;
}

export function responseCount(value: unknown, minimum = 0): number {
  if (
    typeof value !== "number" ||
    !Number.isSafeInteger(value) ||
    value < minimum
  )
    throw new Error("Invalid API count");
  return value;
}

export function responseDate(value: unknown): string {
  const result = responseText(value);
  if (!Number.isFinite(Date.parse(result)))
    throw new Error("Invalid API timestamp");
  return result;
}
