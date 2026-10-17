import type { ReactNode } from "react";
import { useLocation, useParams } from "react-router";
import { KnowledgeOpenLink } from "./KnowledgeOpenLink";
import { useKnowledgeReading } from "./knowledge-reading-context";
import { knowledgePath } from "./knowledge-routes";

export function DocumentMarkdownLink({
  sourceId,
  path,
  hash,
  children,
}: {
  sourceId: string;
  path: string;
  hash: string;
  children: ReactNode;
}) {
  const location = useLocation();
  const route = useParams();
  const reading = useKnowledgeReading();
  const spaceId = reading ? reading.spaceId : route.spaceId;
  const params = new URLSearchParams(location.search);
  params.set("path", path);
  return (
    <KnowledgeOpenLink
      item={{ type: "document", id: sourceId, spaceId, path, hash }}
      to={`${knowledgePath("document", sourceId, spaceId)}/link?${params}${hash}`}
      state={location.state ?? { from: location.pathname + location.search }}
      onKeyDown={(event) => {
        if (event.key === "Enter") event.stopPropagation();
      }}
    >
      {children}
    </KnowledgeOpenLink>
  );
}
