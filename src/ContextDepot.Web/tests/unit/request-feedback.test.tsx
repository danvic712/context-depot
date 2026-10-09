import { describe, expect, test } from "bun:test";
import { AxiosError, AxiosHeaders } from "axios";
import { renderToStaticMarkup } from "react-dom/server";
import { RequestFeedback } from "../../src/components/feedback/RequestFeedback";
import { requestFailure } from "../../src/lib/request-failure";
import { initializeI18n } from "../../src/lib/i18n";

function httpFailure(status: number) {
  return new AxiosError("Raw server error", undefined, undefined, undefined, {
    status,
    statusText: "Error",
    headers: {},
    config: { headers: new AxiosHeaders() },
    data: { message: "不要直接展示的后端错误" },
  });
}
async function language(lang: "en" | "zh") {
  await initializeI18n(lang, async (requested) =>
    Bun.file(
      new URL(
        `../../../../locales/${requested === "zh" ? "zh-CN" : "en-US"}/ui-states.json`,
        import.meta.url,
      ),
    ).json(),
  );
}

describe("Global request feedback", () => {
  test("only recoverable failures offer retries", () => {
    for (const status of [401, 403])
      expect(requestFailure(httpFailure(status))).toEqual({
        kind: "forbidden",
        retryable: false,
      });
    expect(requestFailure(httpFailure(404))).toEqual({
      kind: "notFound",
      retryable: false,
    });
    for (const status of [400, 422])
      expect(requestFailure(httpFailure(status))).toEqual({
        kind: "invalidQuery",
        retryable: false,
      });
    for (const status of [429, 500, 503])
      expect(requestFailure(httpFailure(status))).toEqual({
        kind: "unavailable",
        retryable: true,
      });
    expect(
      requestFailure(new AxiosError("Network Error", "ERR_NETWORK")),
    ).toEqual({ kind: "network", retryable: true });
    expect(requestFailure(new AxiosError("Timeout", "ECONNABORTED"))).toEqual({
      kind: "timeout",
      retryable: true,
    });
    expect(requestFailure(new Error("Invalid JSON"))).toEqual({
      kind: "invalidResponse",
      retryable: true,
    });
  });

  test("a permission error is localized and hides retry and raw server text", async () => {
    for (const lang of ["en", "zh"] as const) {
      await language(lang);
      const html = renderToStaticMarkup(
        <RequestFeedback
          title="Load knowledge"
          failure={requestFailure(httpFailure(403))}
          onRetry={() => {}}
        />,
      );
      expect(html).toContain(lang === "zh" ? "访问" : "access this content");
      expect(html).toContain('role="alert"');
      expect(html).not.toContain("<button");
      expect(html).not.toContain("Raw server error");
      expect(html).not.toContain("不要直接展示的后端错误");
    }
  });

  test("retry keeps stale content context, announces progress, and prevents duplicate clicks", async () => {
    await language("en");
    const html = renderToStaticMarkup(
      <RequestFeedback
        title="Load knowledge"
        failure={requestFailure(httpFailure(503))}
        description="Your query is kept."
        stale
        pending
        onRetry={() => {}}
      />,
    );
    expect(html).toContain('aria-busy="true"');
    expect(html).toContain("disabled");
    expect(html).toContain("Retrying");
    expect(html).toContain("last loaded content");
    expect(html).toContain('role="status"');
    expect(html).toContain("bg-warning-surface");
    expect(html).toContain("Could not update just yet");
    expect(html).not.toContain("Your query is kept.");
    expect(html.match(/last loaded content/g)?.length).toBe(1);
  });

  test("an initial failure keeps its subject and separates the reason from a useful hint", async () => {
    await language("en");
    const html = renderToStaticMarkup(
      <RequestFeedback
        title="Could not finish your search"
        failure={requestFailure(new AxiosError("Offline", "ERR_NETWORK"))}
        description="Your keywords and filters are still here."
        onRetry={() => {}}
      />,
    );
    expect(html).toContain('role="alert"');
    expect(html).toContain("Could not finish your search");
    expect(html).toContain("Check your internet connection");
    expect(html).toContain('<p class="request-feedback-note">Your keywords');
    expect(html).not.toContain("last loaded content");
    expect(html).not.toContain("Could not update just yet");
  });

  test("non-retryable failures offer a recovery action without claiming stale content is visible", async () => {
    await language("en");
    for (const status of [403, 404]) {
      const html = renderToStaticMarkup(
        <RequestFeedback
          title="Could not open this item"
          failure={requestFailure(httpFailure(status))}
          stale
          onRetry={() => {}}
          recoveryAction={{ label: "Back to results", onClick: () => {} }}
        />,
      );
      expect(html).toContain('role="alert"');
      expect(html).toContain("Back to results");
      expect(html.match(/<button/g)?.length).toBe(1);
      expect(html).not.toContain("Retry");
      expect(html).not.toContain("last loaded content");
      expect(html).not.toContain("Could not update just yet");
    }
  });
});
