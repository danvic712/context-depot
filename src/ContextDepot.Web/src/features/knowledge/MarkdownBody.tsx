import Markdown from "react-markdown";
import remarkGfm from "remark-gfm";
import "@/styles/markdown-body.css";

import { safeMarkdownUrl } from "./markdown-url";

export function MarkdownBody({ content }: { content: string }) {
  return (
    <article className="markdown-body">
      <Markdown
        skipHtml
        remarkPlugins={[remarkGfm]}
        urlTransform={safeMarkdownUrl}
        components={{
          h1: ({ children }) => <h2>{children}</h2>,
          h2: ({ children }) => <h3>{children}</h3>,
          h3: ({ children }) => <h4>{children}</h4>,
          h4: ({ children }) => <h5>{children}</h5>,
          h5: ({ children }) => <h6>{children}</h6>,
          a: ({ href, children }) =>
            href ? (
              <a href={href} rel="noopener noreferrer">
                {children}
              </a>
            ) : (
              <span>{children}</span>
            ),
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
