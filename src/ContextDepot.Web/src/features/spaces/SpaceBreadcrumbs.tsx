import { ChevronRightIcon, EllipsisIcon } from "lucide-react";
import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import { Button } from "@/components/ui/button";
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@/components/ui/popover";
import type { SpaceDetail } from "./spaces-api";
import { workspaceDetailTarget } from "./directory-query";

export function SpaceBreadcrumbs({
  detail,
  directory,
}: {
  detail?: SpaceDetail;
  directory: URLSearchParams;
}) {
  const { t } = useTranslation();
  const rootQuery = directory.size ? `?${directory}` : "";
  const ancestors = detail?.ancestors ?? [];
  const earlier = ancestors.slice(0, -1);
  const parent = ancestors.at(-1);
  const ancestorLink = ({ id, name }: (typeof ancestors)[number]) => (
    <Link
      to={workspaceDetailTarget(id, directory, "children")}
      title={name}
      state={{ focusSpace: true }}
    >
      {name}
    </Link>
  );
  return (
    <nav className="space-detail-navigation" aria-label={t("spacesBreadcrumb")}>
      <ol className="space-breadcrumbs">
        <li className="space-breadcrumb-root">
          <Link
            to={`/spaces${rootQuery}`}
            aria-label={t("spacesDirectoryLink")}
          >
            <span className="space-breadcrumb-root-label">
              {t("spacesDirectoryLink")}
            </span>
            <span className="space-breadcrumb-root-short">
              {t("spacesTitle")}
            </span>
          </Link>
        </li>
        {earlier.map((ancestor) => (
          <li className="space-breadcrumb-ancestor" key={ancestor.id}>
            <ChevronRightIcon aria-hidden="true" />
            {ancestorLink(ancestor)}
          </li>
        ))}
        {earlier.length > 0 && (
          <li className="space-breadcrumb-overflow">
            <ChevronRightIcon aria-hidden="true" />
            <Popover>
              <PopoverTrigger asChild>
                <Button
                  type="button"
                  variant="ghost"
                  size="icon"
                  aria-label={t("spacesBreadcrumbMore")}
                  title={t("spacesBreadcrumbMore")}
                >
                  <EllipsisIcon aria-hidden="true" />
                </Button>
              </PopoverTrigger>
              <PopoverContent
                align="start"
                className="space-breadcrumb-popover"
                aria-label={t("spacesBreadcrumbMore")}
              >
                <ul>
                  {earlier.map((ancestor) => (
                    <li key={ancestor.id}>{ancestorLink(ancestor)}</li>
                  ))}
                </ul>
              </PopoverContent>
            </Popover>
          </li>
        )}
        {parent && (
          <li className="space-breadcrumb-parent">
            <ChevronRightIcon aria-hidden="true" />
            {ancestorLink(parent)}
          </li>
        )}
        {detail && (
          <li aria-current="page">
            <ChevronRightIcon aria-hidden="true" />
            <span title={detail.workspace.name}>{detail.workspace.name}</span>
          </li>
        )}
      </ol>
    </nav>
  );
}
