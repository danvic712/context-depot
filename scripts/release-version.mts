import policy from "./version.json" with { type: "json" };

export const DEVELOPMENT_VERSION = policy.developmentVersion;

export function parseReleaseVersion(version: string) {
  const match = new RegExp(policy.releaseVersionPattern).exec(version);
  if (
    !match ||
    match[0] !== version ||
    version.length > 127 ||
    match.slice(1, 4).some((part) => Number(part) > policy.maxAssemblyComponent)
  ) {
    throw new Error(
      "Expected X.Y.Z or X.Y.Z-{alpha|beta|rc|preview}.N, with N >= 1 and .NET-compatible version components.",
    );
  }
  return {
    appVersion: version,
    channel: match[5] ?? "stable",
    isPrerelease: match[5] !== undefined,
  };
}

export function resolveBuildVersion(
  version: string | undefined,
  releaseBuild: boolean,
) {
  if (releaseBuild) return parseReleaseVersion(version ?? "");
  if (version === undefined || version === DEVELOPMENT_VERSION) {
    return {
      appVersion: DEVELOPMENT_VERSION,
      channel: "dev",
      isPrerelease: true,
    };
  }
  return parseReleaseVersion(version);
}
