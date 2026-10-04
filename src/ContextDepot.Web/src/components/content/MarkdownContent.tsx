import Markdown, { type Components } from "react-markdown";
import remarkGfm from "remark-gfm";
import rehypeSlug from "rehype-slug";
import { useId, useRef, type MouseEvent } from "react";
import "@/styles/markdown-body.css";

import { safeMarkdownUrl } from "@/lib/markdown-url";

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
}: {
  content: string;
  nested?: boolean;
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
        container.scrollTop +=
          target.getBoundingClientRect().top -
          container.getBoundingClientRect().top;
        break;
      }
      container = container.parentElement;
    }
    if (!container) target.scrollIntoView({ block: "start" });
    target.tabIndex = -1;
    target.focus({ preventScroll: true });
  }
  return (
    <article className="markdown-body" ref={article}>
      <Markdown
        skipHtml
        remarkPlugins={[remarkGfm]}
        rehypePlugins={[[rehypeSlug, { prefix }]]}
        urlTransform={safeMarkdownUrl}
        components={{
          ...(nested ? nestedHeadings : {}),
          a: ({ href, children }) => {
            const id = href?.startsWith("#") ? fragmentId(href) : undefined;
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
          img: ({ alt }) => <span>{alt}</span>,
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
