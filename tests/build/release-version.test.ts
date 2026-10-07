import { describe, expect, test } from "bun:test";
import {
  parseReleaseVersion,
  resolveBuildVersion,
} from "../../scripts/release-version.mts";

describe("build version policy", () => {
  test("preserves all supported release channels", () => {
    expect(parseReleaseVersion("1.2.3").channel).toBe("stable");
    for (const channel of ["alpha", "beta", "rc", "preview"]) {
      expect(parseReleaseVersion(`1.2.3-${channel}.12`)).toEqual({
        appVersion: `1.2.3-${channel}.12`,
        channel,
        isPrerelease: true,
      });
    }
    expect(parseReleaseVersion("65534.0.0").appVersion).toBe("65534.0.0");
  });
  test("rejects ambiguous or unsupported published versions", () => {
    for (const value of [
      "",
      "v1.2.3",
      "01.2.3",
      "1.2",
      "1.2.3-dev",
      "1.2.3-rc",
      "1.2.3-rc.0",
      "1.2.3-beta.01",
      "1.2.3-nightly.1",
      "1.2.3+sha",
      "65535.0.0",
      "1.2.3\n",
      `1.2.3-rc.${"1".repeat(128)}`,
    ]) {
      expect(() => parseReleaseVersion(value)).toThrow();
    }
  });
  test("development defaults never enter release builds", () => {
    expect(resolveBuildVersion(undefined, false).appVersion).toBe("0.0.0-dev");
    expect(() => resolveBuildVersion(undefined, true)).toThrow();
    expect(() => resolveBuildVersion("0.0.0-dev", true)).toThrow();
    expect(resolveBuildVersion("1.2.3-rc.1", true).appVersion).toBe(
      "1.2.3-rc.1",
    );
  });
});
