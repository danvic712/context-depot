import type { ReactNode } from "react";
import "@/styles/home.css";

export function HomeHeroLayout({
  children,
  search,
}: {
  children: ReactNode;
  search: ReactNode;
}) {
  return (
    <div className="home-hero">
      <div className="home-hero-copy">{children}</div>
      {search}
    </div>
  );
}

export function HomeDashboardLayout({
  children,
  guide,
}: {
  children: ReactNode;
  guide: ReactNode;
}) {
  return (
    <div className="home-dashboard">
      <div className="home-main">{children}</div>
      {guide}
    </div>
  );
}

export function HomeGuideLayout({
  title,
  children,
}: {
  title: ReactNode;
  children: ReactNode;
}) {
  return (
    <aside className="home-about" aria-labelledby="home-about-title">
      <div className="home-section-title">
        <h2 id="home-about-title">{title}</h2>
      </div>
      <div className="home-guide-content">{children}</div>
    </aside>
  );
}
