import { useTranslation } from "react-i18next";
import stillLife from "@/assets/home-still-life.png";
import { NewSpaceCard } from "@/features/spaces/NewSpaceCard";
import { SpaceDirectory } from "@/features/spaces/SpaceDirectory";
import { useSpaceDirectory } from "@/features/spaces/use-space-directory";
import "@/styles/spaces.css";

export function Spaces() {
  const { t } = useTranslation();
  const spaces = useSpaceDirectory();
  return (
    <div className="spaces-workbench">
      <header className="spaces-heading spaces-overview-heading">
        <img className="spaces-hero-art" src={stillLife} alt="" />
        <div>
          <span className="spaces-eyebrow">{t("spacesKicker")}</span>
          <h1>{t("spacesTitle")}</h1>
          <p>{t("spacesSub")}</p>
        </div>
      </header>
      <SpaceDirectory
        resource={spaces}
        root
        creation={<NewSpaceCard onCreated={spaces.onCreated} />}
      />
    </div>
  );
}
