import {
  CircleAlertIcon,
  FileQuestionIcon,
  LockKeyholeIcon,
} from "lucide-react";
import { Link } from "react-router";
import { useTranslation } from "react-i18next";
import { Notice } from "@/components/content/PageElements";
import { Button } from "@/components/ui/button";

export function PageState({
  kind,
}: {
  kind: "error" | "notFound" | "forbidden";
}) {
  const { t } = useTranslation();
  const state = {
    error: {
      icon: CircleAlertIcon,
      title: t("pageLoadError"),
      detail: t("pageLoadErrorWhy"),
    },
    notFound: {
      icon: FileQuestionIcon,
      title: t("pageNotFound"),
      detail: t("pageNotFoundWhy"),
    },
    forbidden: {
      icon: LockKeyholeIcon,
      title: t("pageForbidden"),
      detail: t("requestForbiddenWhy"),
    },
  }[kind];
  return (
    <Notice {...state} heading>
      {kind === "error" && (
        <Button onClick={() => window.location.reload()}>{t("reload")}</Button>
      )}
      <Button asChild variant="outline">
        <Link to="/">{t("pageReturnHome")}</Link>
      </Button>
      <Button asChild variant="outline">
        <Link to="/search">{t("search")}</Link>
      </Button>
    </Notice>
  );
}

export function PageNotFound() {
  return <PageState kind="notFound" />;
}

export function PageLoadError() {
  return <PageState kind="error" />;
}
