export function mcpClientConfiguration(
  url: string,
  secret = "<YOUR_ACCESS_KEY>",
) {
  return JSON.stringify(
    {
      mcpServers: {
        contextdepot: { url, headers: { "X-ContextDepot-Key": secret } },
      },
    },
    null,
    2,
  );
}
