import {
  useLocation,
  useNavigate,
  useParams,
  useSearchParams,
} from "react-router";
import { KnowledgeReadingSession } from "./KnowledgeReadingSession";
import { knowledgeReturnTarget } from "./knowledge-navigation";

// Preserve existing deep links while subsequent reading stays inside this session.
export function DocumentLinkReader() {
  const { knowledgeId = "", spaceId } = useParams();
  const [params] = useSearchParams();
  const location = useLocation();
  const navigate = useNavigate();
  const backParams = new URLSearchParams(params);
  backParams.delete("path");
  const search = backParams.size ? `?${backParams}` : "";
  return (
    <KnowledgeReadingSession
      key={`${spaceId ?? "global"}:${knowledgeId}:${params.get("path") ?? ""}`}
      initial={{
        type: "document",
        id: knowledgeId,
        spaceId,
        path: params.get("path") ?? "",
        hash: location.hash,
      }}
      panel={!!spaceId}
      source={location.state}
      search={search}
      onClose={(deleted = false) => {
        void navigate(
          knowledgeReturnTarget(location.state, spaceId, search).to,
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
