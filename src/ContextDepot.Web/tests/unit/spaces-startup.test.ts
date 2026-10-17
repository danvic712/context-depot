import { expect, test } from "bun:test";

test("Space startup renders its loading layout in a fresh process before i18n initializes", async () => {
  const modulePath = new URL(
    "../../src/components/feedback/RouteFeedback.tsx",
    import.meta.url,
  ).pathname;
  const program = `
    const { createElement } = await import("react");
    const { renderToStaticMarkup } = await import("react-dom/server");
    const { AppLoading } = await import(${JSON.stringify(modulePath)});
    process.stdout.write(renderToStaticMarkup(createElement(AppLoading, { pathname: "/spaces" })));
  `;
  const child = Bun.spawn([process.execPath, "--eval", program], {
    cwd: new URL("../../", import.meta.url).pathname,
    stdout: "pipe",
    stderr: "pipe",
  });
  const [html, errors, exitCode] = await Promise.all([
    new Response(child.stdout).text(),
    new Response(child.stderr).text(),
    child.exited,
  ]);
  if (exitCode !== 0) throw new Error(errors);
  expect(html).toContain("spaces-page-skeleton");
  expect(html).toContain("workspace-list-panel");
  expect(html).not.toContain("spacesFilterPlaceholder");
  expect(html).not.toContain("<button");
});
