import { useLocation, useNavigate, useParams } from "react-router";
import { KnowledgeReadingSession } from "./KnowledgeReadingSession";
import { knowledgeReturnTarget } from "./knowledge-navigation";
import type { KnowledgeType } from "./knowledge-models";
export { KnowledgeReaderView } from "./KnowledgeReaderView";

export function KnowledgeReader({ type }: { type: KnowledgeType }) {
  const { knowledgeId = "", spaceId } = useParams();
  const location = useLocation();
  const navigate = useNavigate();
  return (
    <KnowledgeReadingSession
      key={`${spaceId ?? "global"}:${type}:${knowledgeId}`}
      initial={{ type, id: knowledgeId, spaceId, hash: location.hash }}
      panel={!!spaceId}
      source={location.state}
      search={location.search}
      onClose={(deleted = false) => {
        void navigate(
          knowledgeReturnTarget(location.state, spaceId, location.search).to,
          {
            replace: deleted,
            preventScrollReset: true,
            state: deleted ? { refreshKnowledge: Date.now() } : location.state,
          },
        );
      }}
    />
  );
}
