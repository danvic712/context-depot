import { useTranslation } from "react-i18next";
import {
  CircleCheckIcon,
  CircleAlertIcon,
  InfoIcon,
  Loader2Icon,
  TriangleAlertIcon,
} from "lucide-react";
import { useTheme } from "next-themes";
import { Toaster as Sonner, type ToasterProps } from "sonner";
import "@/styles/toast.css";

const Toaster = ({ ...props }: ToasterProps) => {
  const { theme = "system" } = useTheme();
  const { t } = useTranslation();

  return (
    <Sonner
      theme={theme as ToasterProps["theme"]}
      position="top-right"
      richColors
      swipeDirections={["right"]}
      offset={{ top: "calc(80px + env(safe-area-inset-top))", right: 24 }}
      mobileOffset={{
        top: "calc(72px + env(safe-area-inset-top))",
        right: 12,
        left: 12,
      }}
      className="toaster group"
      containerAriaLabel={t("notifications")}
      toastOptions={{ closeButtonAriaLabel: t("dismissNotification") }}
      icons={{
        success: <CircleCheckIcon className="size-4" />,
        info: <InfoIcon className="size-4" />,
        warning: <TriangleAlertIcon className="size-4" />,
        error: <CircleAlertIcon className="size-4" />,
        loading: <Loader2Icon className="size-4 animate-spin" />,
      }}
      style={
        {
          "--normal-bg": "var(--elevated)",
          "--normal-text": "var(--foreground)",
          "--normal-border": "var(--border)",
          "--success-bg":
            "color-mix(in oklab, var(--primary) 7%, var(--elevated))",
          "--success-border":
            "color-mix(in oklab, var(--primary) 20%, var(--border))",
          "--success-text": "var(--foreground)",
          "--error-bg":
            "color-mix(in oklab, var(--destructive) 7%, var(--elevated))",
          "--error-border":
            "color-mix(in oklab, var(--destructive) 20%, var(--border))",
          "--error-text": "var(--foreground)",
          "--border-radius": "var(--radius)",
        } as React.CSSProperties
      }
      {...props}
    />
  );
};

export { Toaster };
