import { useTranslation } from "react-i18next";
import { Link, useSearchParams } from "react-router";
import { FolderIcon, PlusIcon } from "lucide-react";
import { sampleSpaces } from "../knowledge/sample-data";
import "@/styles/workspace-collection.css";

export function WorkspaceCollection() {
  const { t } = useTranslation();
  const [params] = useSearchParams();
  const suffix = params.get("preview") === "1" ? "?preview=1" : "";
  return (
    <div className="space-grid">
      {sampleSpaces.map((space) => (
        <Link
          className="space-tile"
          key={space.id}
          to={`/spaces/${space.id}${suffix}`}
        >
          <FolderIcon aria-hidden="true" />
          <strong>{space.name}</strong>
          <span className="space-description">{space.description}</span>
          <small>{space.path}</small>
        </Link>
      ))}
      <div className="space-tile unavailable" aria-disabled="true">
        <PlusIcon className="space-create-icon" aria-hidden="true" />
        <strong>{t("newSpace")}</strong>
        <span className="space-description">{t("newSpaceUnavailable")}</span>
      </div>
    </div>
  );
}
