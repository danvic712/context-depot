import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { ArrowUpIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import "@/styles/back-to-top.css";

export function BackToTop() {
  const { t } = useTranslation();
  const [visible, setVisible] = useState(false);
  useEffect(() => {
    const update = () => setVisible(window.scrollY > 400);
    const frame = requestAnimationFrame(update);
    window.addEventListener("scroll", update, { passive: true });
    return () => {
      cancelAnimationFrame(frame);
      window.removeEventListener("scroll", update);
    };
  }, []);

  if (!visible) return null;
  return (
    <Button
      className="back-to-top"
      variant="outline"
      aria-label={t("backToTop")}
      title={t("backToTop")}
      onClick={() => {
        document.getElementById("main-content")?.focus({ preventScroll: true });
        window.scrollTo({
          top: 0,
          behavior: window.matchMedia("(prefers-reduced-motion: reduce)")
            .matches
            ? "instant"
            : "smooth",
        });
      }}
    >
      <ArrowUpIcon aria-hidden="true" />
      <span className="back-to-top-label">{t("backToTop")}</span>
    </Button>
  );
}
