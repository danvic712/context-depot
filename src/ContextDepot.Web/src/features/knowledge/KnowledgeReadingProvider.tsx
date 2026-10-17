import {
  lazy,
  Suspense,
  useCallback,
  useLayoutEffect,
  useRef,
  useState,
  type ReactNode,
} from "react";
import { useLocation } from "react-router";
import {
  KnowledgeReadingContext,
  type ReadingTarget,
} from "./knowledge-reading-context";
const KnowledgeReadingSession = lazy(() =>
  import("./KnowledgeReadingSession").then((module) => ({
    default: module.KnowledgeReadingSession,
  })),
);

export function KnowledgeReadingProvider({
  children,
}: {
  children: ReactNode;
}) {
  const location = useLocation();
  const locationKey = useRef(location.key);
  useLayoutEffect(() => {
    locationKey.current = location.key;
  }, [location.key]);
  const [session, setSession] = useState<{
    target: ReadingTarget;
    origin: HTMLElement | null;
    locationKey: string;
  }>();
  const [revision, setRevision] = useState(0);
  const previousTitle = useRef("");
  const open = useCallback(
    (target: ReadingTarget) => {
      previousTitle.current = document.title;
      setSession({
        target,
        origin:
          document.activeElement instanceof HTMLElement
            ? document.activeElement
            : null,
        locationKey: location.key,
      });
    },
    [location.key],
  );
  const active = session?.locationKey === location.key ? session : undefined;
  return (
    <KnowledgeReadingContext value={{ open, revision }}>
      {children}
      {active && (
        <Suspense fallback={null}>
          <KnowledgeReadingSession
            initial={active.target}
            panel
            onClose={(deleted = false) => {
              setSession(undefined);
              if (locationKey.current !== active.locationKey) return;
              document.title = previousTitle.current;
              if (deleted) setRevision((value) => value + 1);
              if (active.origin?.isConnected)
                active.origin.focus({ preventScroll: true });
            }}
          />
        </Suspense>
      )}
    </KnowledgeReadingContext>
  );
}
