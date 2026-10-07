import { describe, expect, test } from "bun:test";
import { spawnSync } from "node:child_process";

const project = new URL(
  "../../src/ContextDepot.Domain/ContextDepot.Domain.csproj",
  import.meta.url,
).pathname;
function evaluate(...properties: string[]) {
  return spawnSync(
    "dotnet",
    [
      "msbuild",
      project,
      "-nologo",
      "-t:GetAssemblyVersion",
      "-getProperty:AppVersion,Version,InformationalVersion,AssemblyVersion,IncludeSourceRevisionInInformationalVersion",
      ...properties.map((property) => `-p:${property}`),
    ],
    { encoding: "utf8" },
  );
}
describe("MSBuild release version", () => {
  test("local builds use an explicit development version", () => {
    const result = evaluate();
    expect(result.status).toBe(0);
    expect(JSON.parse(result.stdout).Properties.AppVersion).toBe("0.0.0-dev");
  });
  for (const version of [
    "1.2.3",
    "1.2.3-alpha.1",
    "1.2.3-beta.2",
    "1.2.3-rc.3",
    "1.2.3-preview.4",
  ]) {
    test(`all assembly metadata preserves ${version} without v or commit suffix`, () => {
      const result = evaluate(
        "ReleaseBuild=true",
        `AppVersion=${version}`,
        "SourceRevisionId=abcdef",
      );
      expect(result.status).toBe(0);
      const props = JSON.parse(result.stdout).Properties;
      expect(props.AppVersion).toBe(version);
      expect(props.Version).toBe(version);
      expect(props.InformationalVersion).toBe(version);
      expect(props.AssemblyVersion).toBe("1.2.3.0");
      expect(props.IncludeSourceRevisionInInformationalVersion).toBe("false");
    });
  }
  for (const version of [
    undefined,
    "0.0.0-dev",
    "v1.2.3",
    "1.2.3-rc.0",
    "1.2.3+sha",
    "65535.0.0",
  ]) {
    test(`release builds reject ${version ?? "a missing version"}`, () => {
      const result = evaluate(
        "ReleaseBuild=true",
        ...(version === undefined ? [] : [`AppVersion=${version}`]),
      );
      expect(result.status).not.toBe(0);
    });
  }
  test("rejects independent version overrides and automatic source suffixes", () => {
    for (const property of [
      "Version=2.0.0",
      "InformationalVersion=2.0.0",
      "IncludeSourceRevisionInInformationalVersion=true",
    ]) {
      expect(
        evaluate("ReleaseBuild=true", "AppVersion=1.2.3", property).status,
      ).not.toBe(0);
    }
  });
});
