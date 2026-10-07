#:property ImportDirectoryBuildProps=false
#:property ImportDirectoryBuildTargets=false
#:property PublishAot=false
#:property Nullable=enable
#:property TreatWarningsAsErrors=true

using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

try
{
    var policy = ReadJson(Path.Combine(SourceDirectory(), "version.json"));
    var operation = args.FirstOrDefault() ?? "";
    if (operation == "verify-artifacts")
    {
        if (args.Length != 3)
            throw new InvalidOperationException("Expected verify-artifacts <publish-directory> <app-version>.");
        var version = ParseVersion(args[2], policy);
        var frontend = ReadJson(Path.Combine(args[1], "wwwroot/version.json"));
        if (Text(frontend, "version") != version.Value || Text(frontend, "channel") != version.Channel)
            throw new InvalidOperationException("Published frontend metadata does not match AppVersion. Rebuild frontend assets with the same version.");
        foreach (var name in new[] { "ContextDepot", "ContextDepot.Application", "ContextDepot.Domain", "ContextDepot.Infrastructure" })
            if (ReadInformationalVersion(Path.Combine(args[1], name + ".dll")) != version.Value)
                throw new InvalidOperationException($"{name} published InformationalVersion does not match AppVersion. Rebuild all projects with the same version.");
        Console.WriteLine($"Verified frontend metadata and four assemblies: {version.Value}");
        return 0;
    }

    if (operation is not ("resolve" or "prepare" or "record" or "promote"))
        throw new InvalidOperationException("Expected resolve, prepare, record, promote or verify-artifacts.");
    var release = ReadJson(RequiredEnvironment("GITHUB_EVENT_PATH"))["release"]
        ?? throw new InvalidOperationException("Expected a published GitHub Release.");
    var tag = Text(release, "tag_name");
    var id = release["id"]?.GetValue<long>() ?? 0;
    var prerelease = Flag(release, "prerelease");
    if (id <= 0 || Flag(release, "draft") || tag.Length > 128)
        throw new InvalidOperationException("Expected a published GitHub Release with a valid tag.");
    var appVersion = ParseVersion(tag.StartsWith('v') ? tag[1..] : tag, policy);
    if ((appVersion.Channel != "stable") != prerelease)
        throw new InvalidOperationException("Release tag and GitHub prerelease flag must agree.");
    var revision = (await Run("git", "rev-parse", "HEAD")).RequireSuccess().Trim();
    if (!Regex.IsMatch(revision, "^[a-f0-9]{40}$") || revision != RequiredEnvironment("GITHUB_SHA"))
        throw new InvalidOperationException("Release checkout does not match the event commit.");
    var context = new ReleaseContext(tag, id, appVersion.Value, appVersion.Channel, revision);

    if (operation == "resolve")
    {
        Output("app_version", context.Version);
        Output("channel", context.Channel);
        Output("revision", context.Revision);
        return 0;
    }
    var images = new[] { RequiredEnvironment("DOCKERHUB_IMAGE"), RequiredEnvironment("GHCR_IMAGE") };

    if (operation == "prepare")
    {
        var current = new JsonNode?[images.Length];
        string? seed = null;
        string? seedDigest = null;
        var alias = tag.StartsWith('v') ? tag[1..] : "v" + tag;
        for (var index = 0; index < images.Length; index++)
        {
            current[index] = await Inspect(images[index] + ":" + tag);
            var alternative = await Inspect(images[index] + ":" + alias);
            foreach (var candidate in new[] { current[index], alternative })
            {
                if (candidate is null) continue;
                var digest = VerifyImage(candidate, context);
                if (seedDigest is not null && seedDigest != digest)
                    throw new InvalidOperationException("Existing tags for this application version have different digests.");
                seedDigest = digest;
                seed = images[index] + "@" + digest;
            }
        }
        if (seed is null)
            Output("build_required", "true");
        else
        {
            for (var index = 0; index < images.Length; index++)
            {
                if (current[index] is not null) continue;
                var authDirectory = Environment.GetEnvironmentVariable("DOCKER_CONFIG")
                    ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".docker");
                // Copy every platform and attestation without rebuilding a partially published release.
                (await Run("skopeo", "copy", "--all", "--preserve-digests",
                    "--authfile", Path.Combine(authDirectory, "config.json"),
                    "docker://" + seed, "docker://" + images[index] + ":" + tag)).RequireSuccess();
                if (VerifyImage(await Inspect(images[index] + ":" + tag), context) != seedDigest)
                    throw new InvalidOperationException("Registry copy did not preserve the release digest.");
            }
            Output("build_required", "false");
        }
    }
    else if (operation == "record")
    {
        var first = VerifyImage(await Inspect(images[0] + ":" + tag), context);
        var second = VerifyImage(await Inspect(images[1] + ":" + tag), context);
        if (first != second)
            throw new InvalidOperationException("Registries contain different release digests.");
        Output("dockerhub_digest", first);
        Output("ghcr_digest", second);
    }
    else if (operation == "promote")
    {
        if (context.Channel != "stable")
        {
            Console.WriteLine("Skipped latest for a prerelease.");
            return 0;
        }
        var digests = new[] { RequiredEnvironment("DOCKERHUB_DIGEST"), RequiredEnvironment("GHCR_DIGEST") };
        for (var index = 0; index < images.Length; index++)
        {
            if (!IsDigest(digests[index]) ||
                VerifyImage(await Inspect(images[index] + "@" + digests[index]), context) != digests[index])
                throw new InvalidOperationException("Release digest verification failed.");
        }
        if (digests[0] != digests[1])
            throw new InvalidOperationException("Registries contain different release digests.");
        using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        for (var index = 0; index < images.Length; index++)
        {
            if (!await IsLatest(client, context))
            {
                Console.WriteLine("Skipped latest: this is not the current latest stable Release.");
                return 0;
            }
            (await Run("docker", "buildx", "imagetools", "create", "--tag",
                images[index] + ":latest", images[index] + "@" + digests[index])).RequireSuccess();
        }
        Console.WriteLine("Promoted verified release digests to latest.");
    }
    return 0;
}
catch (Exception error)
{
    Console.Error.WriteLine("Release tooling failed: " + error.Message);
    return 1;
}

static string SourceDirectory([CallerFilePath] string source = "") => Path.GetDirectoryName(source)!;
static JsonNode ReadJson(string path) => JsonNode.Parse(File.ReadAllText(path))
    ?? throw new InvalidOperationException("Empty JSON file: " + path);
static string Text(JsonNode value, string property) => value[property]?.GetValue<string>()
    ?? throw new InvalidOperationException("Missing JSON property: " + property);
static bool Flag(JsonNode value, string property) => value[property]?.GetValue<bool>()
    ?? throw new InvalidOperationException("Missing JSON property: " + property);
static string RequiredEnvironment(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
    ? value : throw new InvalidOperationException(name + " is required.");
static void Output(string name, string value) => File.AppendAllText(RequiredEnvironment("GITHUB_OUTPUT"), $"{name}={value}\n");
static bool IsDigest(string value) => Regex.IsMatch(value, @"\Asha256:[a-f0-9]{64}\z");

static ReleaseVersion ParseVersion(string value, JsonNode policy)
{
    var match = Regex.Match(value, Text(policy, "releaseVersionPattern"), RegexOptions.CultureInvariant);
    var limit = policy["maxAssemblyComponent"]!.GetValue<int>();
    if (!match.Success || match.Value != value || value.Length > 127 ||
        Enumerable.Range(1, 3).Any(index =>
            !int.TryParse(match.Groups[index].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var part) || part > limit))
        throw new InvalidOperationException("Expected X.Y.Z or X.Y.Z-{alpha|beta|rc|preview}.N, with N >= 1 and .NET-compatible components. dev images cannot be published.");
    return new ReleaseVersion(value, match.Groups[5].Success ? match.Groups[5].Value : "stable");
}

static async Task<JsonNode?> Inspect(string reference)
{
    var result = await Run("docker", "buildx", "imagetools", "inspect", reference, "--format", "{{json .Manifest}}");
    if (result.ExitCode == 0)
        return JsonNode.Parse(result.StandardOutput) ?? throw new InvalidOperationException("Empty registry manifest.");
    if (!Regex.IsMatch(result.StandardError, "unauthorized|denied|forbidden", RegexOptions.IgnoreCase) &&
        Regex.IsMatch(result.StandardError, "manifest unknown|: not found|no such manifest", RegexOptions.IgnoreCase))
        return null;
    throw new InvalidOperationException("Registry inspection failed; refusing to treat it as a missing image. " + result.StandardError.Trim());
}

static string VerifyImage(JsonNode? manifest, ReleaseContext context)
{
    var annotations = manifest?["annotations"];
    var platforms = manifest?["manifests"]?.AsArray()
        .Where(item => item?["platform"]?["os"]?.GetValue<string>() == "linux")
        .Select(item => item?["platform"]?["architecture"]?.GetValue<string>()).ToArray() ?? [];
    var digest = manifest?["digest"]?.GetValue<string>() ?? "";
    if (!IsDigest(digest) ||
        annotations?["org.opencontainers.image.version"]?.GetValue<string>() != context.Version ||
        annotations?["org.opencontainers.image.revision"]?.GetValue<string>() != context.Revision ||
        !platforms.Contains("amd64") || !platforms.Contains("arm64"))
        throw new InvalidOperationException("Release image is missing, incomplete or has conflicting version/revision metadata. Existing tags must not be overwritten.");
    return digest;
}

static async Task<bool> IsLatest(HttpClient client, ReleaseContext context)
{
    var api = (Environment.GetEnvironmentVariable("GITHUB_API_URL") ?? "https://api.github.com").TrimEnd('/');
    using var request = new HttpRequestMessage(HttpMethod.Get,
        api + "/repos/" + RequiredEnvironment("GITHUB_REPOSITORY") + "/releases/latest");
    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", RequiredEnvironment("GITHUB_TOKEN"));
    request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
    request.Headers.UserAgent.ParseAdd("ContextDepot-release-tool");
    using var response = await client.SendAsync(request);
    if (response.StatusCode == HttpStatusCode.NotFound) return false;
    response.EnsureSuccessStatusCode();
    var release = JsonNode.Parse(await response.Content.ReadAsStringAsync())
        ?? throw new InvalidOperationException("Empty GitHub latest Release response.");
    var id = release["id"]?.GetValue<long>() ?? throw new InvalidOperationException("Missing latest Release ID.");
    var tag = Text(release, "tag_name");
    var draft = Flag(release, "draft");
    var prerelease = Flag(release, "prerelease");
    return id == context.Id && tag == context.Tag && !draft && !prerelease;
}

static string? ReadInformationalVersion(string path)
{
    // Read PE metadata without loading or executing the published application.
    using var stream = File.OpenRead(path);
    using var pe = new PEReader(stream);
    var reader = pe.GetMetadataReader();
    foreach (var handle in reader.GetAssemblyDefinition().GetCustomAttributes())
    {
        var attribute = reader.GetCustomAttribute(handle);
        var type = attribute.Constructor.Kind switch
        {
            HandleKind.MemberReference => reader.GetMemberReference((MemberReferenceHandle)attribute.Constructor).Parent,
            HandleKind.MethodDefinition => reader.GetMethodDefinition((MethodDefinitionHandle)attribute.Constructor).GetDeclaringType(),
            _ => default(EntityHandle)
        };
        if (type.Kind != HandleKind.TypeReference) continue;
        var reference = reader.GetTypeReference((TypeReferenceHandle)type);
        if (reader.GetString(reference.Namespace) != "System.Reflection" ||
            reader.GetString(reference.Name) != "AssemblyInformationalVersionAttribute") continue;
        var blob = reader.GetBlobReader(attribute.Value);
        if (blob.ReadUInt16() != 1) throw new InvalidOperationException("Invalid assembly version attribute: " + path);
        return blob.ReadSerializedString();
    }
    return null;
}

static async Task<CommandResult> Run(string command, params string[] arguments)
{
    var start = new ProcessStartInfo(command) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
    foreach (var argument in arguments) start.ArgumentList.Add(argument);
    using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start " + command);
    var output = process.StandardOutput.ReadToEndAsync();
    var error = process.StandardError.ReadToEndAsync();
    using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
    try { await process.WaitForExitAsync(timeout.Token); }
    catch (OperationCanceledException) { process.Kill(entireProcessTree: true); throw new TimeoutException(command + " timed out."); }
    return new CommandResult(command, process.ExitCode, await output, await error);
}

record ReleaseVersion(string Value, string Channel);
record ReleaseContext(string Tag, long Id, string Version, string Channel, string Revision);
record CommandResult(string Command, int ExitCode, string StandardOutput, string StandardError)
{
    public string RequireSuccess() => ExitCode == 0 ? StandardOutput
        : throw new InvalidOperationException($"{Command} failed ({ExitCode}): {StandardError.Trim()}");
}
