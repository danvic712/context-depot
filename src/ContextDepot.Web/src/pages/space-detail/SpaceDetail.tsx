import { ItemGroup } from "@/components/ui/item";
import { useAppContext } from "@/hooks/use-app-context";
import { useTranslation } from "react-i18next";
import { FileTextIcon, FolderIcon } from "lucide-react";
import "@/styles/space-detail.css";
import { Button } from "@/components/ui/button";
import { ToggleGroup, ToggleGroupItem } from "@/components/ui/toggle-group";
import { Notice, Heading } from "@/components/content/PageElements";
import { StatePreview } from "@/components/feedback/StatePreview";
import { SampleRow } from "@/features/knowledge/KnowledgeRow";
import {
  sampleSpaces,
  sampleKnowledge,
} from "@/features/knowledge/sample-data";

export function SpaceDetail() {
  const { preview, state, onRetry, selectedId, view, navigate } =
    useAppContext();
  const { t } = useTranslation();
  const selectedSpace = sampleSpaces.find((space) => space.id === selectedId);
  const selectedView = ["all", "contexts", "documents"].includes(view)
    ? view
    : "all";
  const results = sampleKnowledge.filter(
    (item) =>
      item.workspaceId === selectedId &&
      (selectedView === "all" ||
        item.type === (selectedView === "contexts" ? "context" : "document")),
  );

  return (
    <>
      <Button
        variant="ghost"
        className="back-link"
        onClick={() => navigate("/spaces")}
      >
        ← {t("spaces")}
      </Button>
      {preview && state !== "success" && state !== "degraded" ? (
        <StatePreview state={state} onRetry={onRetry} />
      ) : preview && selectedSpace ? (
        <>
          <Heading
            kicker={t("sampleSpace")}
            title={selectedSpace.name}
            sub={selectedSpace.description}
          />
          <p className="path-label">{selectedSpace.path}</p>
          <section className="results">
            <ToggleGroup
              type="single"
              value={selectedView}
              onValueChange={(value) =>
                value &&
                navigate(
                  `/spaces/${selectedSpace.id}${value === "all" ? "" : `?view=${value}`}`,
                )
              }
              variant="outline"
              spacing={2}
              aria-label={t("allKnowledge")}
              className="view-tabs"
            >
              {[
                ["all", t("allKnowledge")],
                ["contexts", t("contexts")],
                ["documents", t("documentsFilter")],
              ].map(([value, label]) => (
                <ToggleGroupItem key={value} value={value}>
                  {label}
                </ToggleGroupItem>
              ))}
            </ToggleGroup>

            {state === "degraded" && (
              <StatePreview state={state} onRetry={onRetry} />
            )}
            {results.length ? (
              <ItemGroup className="divide-y border-y">
                {results.map((item) => (
                  <SampleRow key={item.id} item={item} />
                ))}
              </ItemGroup>
            ) : (
              <Notice
                icon={FileTextIcon}
                title={t("noKnowledge")}
                detail={t("changeView")}
              >
                <Button
                  type="button"
                  variant="outline"
                  onClick={() => navigate(`/spaces/${selectedSpace.id}`)}
                >
                  {t("allKnowledge")}
                </Button>
              </Notice>
            )}
          </section>
        </>
      ) : (
        <Notice
          icon={FolderIcon}
          title={t("spacesMissing")}
          detail={t("spacesWhy")}
        />
      )}
    </>
  );
}
