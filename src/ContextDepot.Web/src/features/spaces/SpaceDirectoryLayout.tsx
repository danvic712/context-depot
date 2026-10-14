import type { ComponentProps } from "react";
import { useTranslation } from "react-i18next";
import {
  PaginationBar,
  type PaginationBarProps,
} from "@/components/content/PaginationBar";
import { paginationDefaults } from "@/lib/pagination";
import configuredI18n from "@/lib/i18n";

export function SpaceDirectoryHeading({ filtered }: { filtered: boolean }) {
  const { t } = useTranslation(undefined, {
    i18n: configuredI18n,
    useSuspense: false,
  });
  return (
    <h2 className="sr-only" id="space-directory-title">
      {t(filtered ? "spacesMatchingSpaces" : "spacesAllSpaces")}
    </h2>
  );
}

export function SpaceDirectoryFooter({
  totalCount,
  page = 1,
  pageSize = paginationDefaults.pageSize,
  itemCount = 0,
  ...props
}: PaginationBarProps) {
  const { t, i18n, ready } = useTranslation(undefined, {
    i18n: configuredI18n,
    useSuspense: false,
  });
  const number = new Intl.NumberFormat(i18n.resolvedLanguage);
  const from = (page - 1) * pageSize + 1;
  const to = Math.min(from + itemCount - 1, totalCount ?? 0);
  const summary =
    totalCount === undefined
      ? undefined
      : itemCount > 0
        ? t("spacesResultRange", {
            count: totalCount,
            from: number.format(from),
            to: number.format(to),
            total: number.format(totalCount),
          })
        : t("spacesResultCount", {
            count: totalCount,
            value: number.format(totalCount),
          });
  return (
    <PaginationBar
      {...props}
      className="space-directory-footer"
      totalCount={totalCount}
      page={page}
      pageSize={pageSize}
      itemCount={itemCount}
      summary={summary}
      ariaLabel={ready ? t("spacesPagination") : undefined}
    />
  );
}

export function WorkspaceListPanel(
  props: Omit<ComponentProps<"div">, "className">,
) {
  return <div className="workspace-list-panel" {...props} />;
}
