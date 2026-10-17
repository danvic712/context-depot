import { httpRequest } from "@/lib/http-client";
import { responseRecord, responseText } from "@/lib/response-values";

export function resolveDocumentLink(
  sourceId: string,
  path: string,
  spaceId?: string,
  signal?: AbortSignal,
) {
  return httpRequest({
    method: "GET",
    url: `/knowledge/document/${encodeURIComponent(sourceId)}/link`,
    params: { path, workspaceId: spaceId },
    signal,
    parse: (data) => {
      const id = responseText(responseRecord(data).id);
      if (!/^[\da-f]{8}(?:-[\da-f]{4}){3}-[\da-f]{12}$/i.test(id))
        throw new Error("Invalid document link response");
      return id;
    },
  });
}
