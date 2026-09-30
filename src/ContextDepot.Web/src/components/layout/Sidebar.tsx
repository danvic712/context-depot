import { useTranslation } from "react-i18next";
import { NavLink, type To } from "react-router";
import { Brand } from "../Brand";
import {
  HouseIcon,
  SearchIcon,
  FolderIcon,
  ClockIcon,
  SettingsIcon,
} from "lucide-react";
import { cn } from "@/lib/utils";
import "@/styles/sidebar.css";

interface Props {
  linkTo: (path: string) => To;
}

export function Sidebar({ linkTo }: Props) {
  const { t } = useTranslation();
  return (
    <aside className="rail" aria-label={t("primaryNavigation")}>
      <div className="rail-brand">
        <Brand />
      </div>
      <nav className="rail-links">
        {(
          [
            { item: "home", Icon: HouseIcon },
            { item: "search", Icon: SearchIcon },
            { item: "spaces", Icon: FolderIcon },
          ] as const
        ).map(({ item, Icon }) => (
          <NavLink
            key={item}
            to={linkTo(item === "home" ? "/" : `/${item}`)}
            end={item === "home"}
            className={({ isActive }) =>
              cn("rail-link", isActive && "selected")
            }
          >
            <Icon size={22} aria-hidden="true" />
            <span>{t(item)}</span>
          </NavLink>
        ))}
        <span
          className="rail-link disabled"
          title={t("timelineWhy")}
          aria-disabled="true"
        >
          <ClockIcon size={22} aria-hidden="true" />
          <span>{t("timeline")}</span>
        </span>
      </nav>
      <div className="rail-bottom">
        <NavLink
          to={linkTo("/settings")}
          className={({ isActive }) => cn("rail-link", isActive && "selected")}
        >
          <SettingsIcon size={22} aria-hidden="true" />
          <span>{t("settings")}</span>
        </NavLink>
        <div className="rail-status">
          <span className="status-dot" />
          {t("unknown")}
        </div>
      </div>
    </aside>
  );
}
