import Markdown, { type Components } from "react-markdown";
import remarkGfm from "remark-gfm";
import { rehypeMarkdownHeadings } from "@/lib/markdown-headings";
import {
  useEffect,
  useLayoutEffect,
  useEffectEvent,
  useId,
  useRef,
  type MouseEvent,
  type ReactNode,
} from "react";
import "@/styles/markdown-body.css";

import { safeMarkdownUrl } from "@/lib/markdown-url";
import { MarkdownCodeBlock } from "./MarkdownCodeBlock";

const nestedHeadings: Components = {
  h1: ({ children, id }) => <h2 id={id}>{children}</h2>,
  h2: ({ children, id }) => <h3 id={id}>{children}</h3>,
  h3: ({ children, id }) => <h4 id={id}>{children}</h4>,
  h4: ({ children, id }) => <h5 id={id}>{children}</h5>,
  h5: ({ children, id }) => <h6 id={id}>{children}</h6>,
};

export function MarkdownContent({
  content,
  nested = false,
  renderLink,
  fragment,
}: {
  content: string;
  nested?: boolean;
  renderLink?: (href: string, children: ReactNode) => ReactNode;
  fragment?: string;
}) {
  const prefix = `markdown-${useId()}-`;
  const article = useRef<HTMLElement>(null);
  function fragmentId(href: string) {
    try {
      return `${prefix}${decodeURIComponent(href.slice(1))}`;
    } catch {
      return undefined;
    }
  }
  function followFragment(event: MouseEvent<HTMLAnchorElement>, id: string) {
    if (
      event.button !== 0 ||
      event.metaKey ||
      event.ctrlKey ||
      event.shiftKey ||
      event.altKey
    )
      return;
    event.preventDefault();
    scrollToFragment(id, true);
  }
  function scrollToFragment(id: string, smooth = false) {
    const target = Array.from(
      article.current?.querySelectorAll<HTMLElement>("[id]") ?? [],
    ).find((heading) => heading.id === id);
    if (!target) return;
    // Scroll only the current reader pane when it owns scrolling.
    let container = article.current?.parentElement;
    while (container) {
      if (
        /auto|scroll/.test(getComputedStyle(container).overflowY) &&
        container.scrollHeight > container.clientHeight
      ) {
        const top =
          container.scrollTop +
          target.getBoundingClientRect().top -
          container.getBoundingClientRect().top;
        container.scrollTo({
          top,
          behavior:
            smooth &&
            !window.matchMedia("(prefers-reduced-motion: reduce)").matches
              ? "smooth"
              : "instant",
        });
        break;
      }
      container = container.parentElement;
    }
    if (!container)
      target.scrollIntoView({
        block: "start",
        behavior:
          smooth &&
          !window.matchMedia("(prefers-reduced-motion: reduce)").matches
            ? "smooth"
            : "instant",
      });
    target.tabIndex = -1;
    target.focus({ preventScroll: true });
  }
  const followLocationFragment = useEffectEvent(() => {
    const hash = fragment ?? window.location.hash;
    if (hash) {
      const id = fragmentId(hash);
      if (id) scrollToFragment(id);
    }
  });
  useLayoutEffect(() => {
    followLocationFragment();
  }, [content, fragment]);
  useEffect(() => {
    if (fragment !== undefined) return;
    window.addEventListener("hashchange", followLocationFragment);
    return () =>
      window.removeEventListener("hashchange", followLocationFragment);
  }, [content, fragment]);
  return (
    <article className="markdown-body" ref={article}>
      <Markdown
        skipHtml
        remarkPlugins={[remarkGfm]}
        rehypePlugins={[[rehypeMarkdownHeadings, { prefix }]]}
        urlTransform={safeMarkdownUrl}
        components={{
          ...(nested ? nestedHeadings : {}),
          a: ({ href, children }) => {
            const id = href?.startsWith("#") ? fragmentId(href) : undefined;
            const custom =
              href && !id ? renderLink?.(href, children) : undefined;
            if (custom !== undefined) return custom;
            return href ? (
              <a
                href={id ? `#${id}` : href}
                rel="noopener noreferrer"
                onClick={id ? (event) => followFragment(event, id) : undefined}
                onKeyDown={
                  id
                    ? (event) => {
                        // A reader link must keep Enter when nested inside cmdk.
                        if (event.key === "Enter") event.stopPropagation();
                      }
                    : undefined
                }
              >
                {children}
              </a>
            ) : (
              <span>{children}</span>
            );
          },
          img: ({ src, alt }) =>
            src ? (
              <img
                src={src}
                alt={alt ?? ""}
                loading="lazy"
                decoding="async"
                referrerPolicy="no-referrer"
              />
            ) : (
              <span>{alt}</span>
            ),
          pre: ({ children }) => (
            <MarkdownCodeBlock>{children}</MarkdownCodeBlock>
          ),
          table: ({ children }) => (
            <div className="markdown-table">
              <table>{children}</table>
            </div>
          ),
        }}
      >
        {content}
      </Markdown>
    </article>
  );
}
