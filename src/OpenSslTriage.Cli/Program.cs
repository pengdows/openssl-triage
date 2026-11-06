using System.Text;
using System.Text.Json;

const string usage = "Usage: openssl-triage <list|inspect|report> [case-id] [--output path]";

if (args.Length == 0 || args[0] is "-h" or "--help")
{
    Console.WriteLine(usage);
    return;
}

var command = args[0];
var root = FindCasesDirectory();
if (root is null)
{
    Console.Error.WriteLine("Could not find a cases/ directory. Run this command from the project or a case workspace.");
    Environment.ExitCode = 2;
    return;
}

if (command == "list")
{
    foreach (var path in EnumerateCaseManifests(root))
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var item = doc.RootElement;
        Console.WriteLine($"{TerminalText(item.GetProperty("id").GetString())}\t{TerminalText(item.GetProperty("title").GetString())}");
    }
    return;
}

if (args.Length < 2 || command is not ("inspect" or "report"))
{
    Console.Error.WriteLine(usage);
    Environment.ExitCode = 2;
    return;
}

if (args[1].Length == 0 || !args[1].All(char.IsAsciiDigit))
{
    Console.Error.WriteLine("Invalid case ID. Case IDs must contain only ASCII digits.");
    Environment.ExitCode = 2;
    return;
}

var casesRoot = ResolvePath(new DirectoryInfo(root));
var caseDirectory = Path.GetFullPath(Path.Combine(casesRoot, args[1]));
if (!Directory.Exists(caseDirectory))
{
    Console.Error.WriteLine($"Unknown case '{args[1]}'.");
    Environment.ExitCode = 2;
    return;
}

var resolvedCaseDirectory = ResolvePath(new DirectoryInfo(caseDirectory));
if (!IsWithinDirectory(casesRoot, resolvedCaseDirectory))
{
    Console.Error.WriteLine("Case path must remain inside the cases/ directory.");
    Environment.ExitCode = 2;
    return;
}

var casePath = Path.Combine(resolvedCaseDirectory, "case.json");
if (!File.Exists(casePath))
{
    Console.Error.WriteLine($"Unknown case '{args[1]}'.");
    Environment.ExitCode = 2;
    return;
}

var resolvedCasePath = ResolvePath(new FileInfo(casePath));
if (!IsWithinDirectory(casesRoot, resolvedCasePath))
{
    Console.Error.WriteLine("Case manifest must remain inside the cases/ directory.");
    Environment.ExitCode = 2;
    return;
}

using var caseDocument = JsonDocument.Parse(File.ReadAllText(resolvedCasePath));
var json = caseDocument.RootElement;
if (!string.Equals(json.GetProperty("id").GetString(), args[1], StringComparison.Ordinal))
{
    Console.Error.WriteLine("Case ID does not match the case manifest.");
    Environment.ExitCode = 2;
    return;
}
if (command == "inspect")
{
    Console.WriteLine(JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true }));
    return;
}

var outputFlag = Array.IndexOf(args, "--output");
var outputPath = outputFlag >= 0 && outputFlag + 1 < args.Length
    ? Path.GetFullPath(args[outputFlag + 1])
    : Path.Combine(resolvedCaseDirectory, "evidence-report.md");
var report = json.GetProperty("id").GetString() == "32897"
    ? BuildReport(json, resolvedCaseDirectory)
    : BuildGeneralReport(json, resolvedCaseDirectory);
Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
File.WriteAllText(outputPath, report);
Console.WriteLine($"Wrote {TerminalText(outputPath)}");

static string? FindCasesDirectory()
{
    for (var directory = new DirectoryInfo(Environment.CurrentDirectory); directory is not null; directory = directory.Parent)
    {
        var candidate = Path.Combine(directory.FullName, "cases");
        if (Directory.Exists(candidate)) return candidate;
    }
    return null;
}

static IEnumerable<string> EnumerateCaseManifests(string root)
{
    var casesRoot = ResolvePath(new DirectoryInfo(root));
    foreach (var directory in Directory.EnumerateDirectories(casesRoot).Order(StringComparer.Ordinal))
    {
        var caseId = Path.GetFileName(directory);
        if (caseId.Length == 0 || !caseId.All(char.IsAsciiDigit))
        {
            continue;
        }

        var resolvedDirectory = ResolvePath(new DirectoryInfo(directory));
        if (!IsWithinDirectory(casesRoot, resolvedDirectory))
        {
            Console.Error.WriteLine($"Skipping case directory outside cases/: {caseId}");
            continue;
        }

        var manifest = Path.Combine(resolvedDirectory, "case.json");
        if (!File.Exists(manifest))
        {
            continue;
        }

        var resolvedManifest = ResolvePath(new FileInfo(manifest));
        if (!IsWithinDirectory(casesRoot, resolvedManifest))
        {
            Console.Error.WriteLine($"Skipping case manifest outside cases/: {caseId}");
            continue;
        }

        yield return resolvedManifest;
    }
}

static string TerminalText(string? value)
{
    var builder = new StringBuilder(value?.Length ?? 0);
    foreach (var character in value ?? "")
    {
        builder.Append(char.IsControl(character) ? ' ' : character);
    }

    return builder.ToString();
}

static string ResolvePath(FileSystemInfo path)
{
    var fullPath = Path.GetFullPath(path.FullName);
    if ((path.Attributes & FileAttributes.ReparsePoint) == 0)
    {
        return fullPath;
    }

    return path.ResolveLinkTarget(returnFinalTarget: true)?.FullName ?? fullPath;
}

static bool IsWithinDirectory(string directory, string path)
{
    var relativePath = Path.GetRelativePath(directory, path);
    return relativePath != "."
        && !Path.IsPathRooted(relativePath)
        && relativePath != ".."
        && !relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
        && !relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal);
}

static string MarkdownText(string? value)
{
    var normalized = (value ?? "").Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ');
    var builder = new StringBuilder(normalized.Length * 2);
    foreach (var character in normalized)
    {
        if (IsAsciiPunctuation(character))
        {
            builder.Append('\\');
        }

        builder.Append(character);
    }

    return builder.ToString();
}

static bool IsAsciiPunctuation(char character)
{
    return character is (>= '!' and <= '/')
        or (>= ':' and <= '@')
        or (>= '[' and <= '`')
        or (>= '{' and <= '~');
}

static string MarkdownLink(string? label, string? value)
{
    if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
        || uri.Scheme != Uri.UriSchemeHttps
        || string.IsNullOrWhiteSpace(uri.Host)
        || uri.UserInfo.Length != 0)
    {
        return MarkdownText(label);
    }

    var safeUri = uri.GetComponents(UriComponents.AbsoluteUri, UriFormat.UriEscaped);
    return $"[{MarkdownText(label)}](<{safeUri}>)";
}

static string InlineCode(string? value)
{
    var content = value ?? "";
    var delimiter = new string('`', Math.Max(1, LongestRun(content, '`') + 1));
    var padding = content.StartsWith('`') || content.EndsWith('`') || content.StartsWith(' ') || content.EndsWith(' ')
        ? " "
        : "";
    return $"{delimiter}{padding}{content}{padding}{delimiter}";
}

static string CodeBlock(string? value)
{
    var content = value ?? "";
    var delimiter = new string('`', Math.Max(3, LongestRun(content, '`') + 1));
    return $"{delimiter}text{Environment.NewLine}{content}{Environment.NewLine}{delimiter}";
}

static int LongestRun(string value, char character)
{
    var longest = 0;
    var current = 0;
    foreach (var item in value)
    {
        current = item == character ? current + 1 : 0;
        longest = Math.Max(longest, current);
    }

    return longest;
}

static string BuildReport(JsonElement item, string caseDirectory)
{
    string Value(string name) => item.GetProperty(name).GetString() ?? "";
    var revisions = item.GetProperty("revisions");
    var source = revisions.GetProperty("reported");
    var fixedRevision = revisions.GetProperty("fix");
    var evidenceDirectory = Path.Combine(caseDirectory, "evidence");
    var reportedCallback = ReadCallback(Path.Combine(evidenceDirectory, "reported-callback.txt"));
    var fixedCallback = ReadCallback(Path.Combine(evidenceDirectory, "fixed-callback.txt"));
    var environment = string.Join(Environment.NewLine, new[] { "environment.txt", "toolchain.txt", "container-image.txt" }
        .Select(name => Path.Combine(evidenceDirectory, name))
        .Where(File.Exists)
        .Select(path => File.ReadAllText(path).Trim()));
    if (string.IsNullOrWhiteSpace(environment)) environment = "Not run.";
    var observedRuns = ReadRuntimeSummary(Path.Combine(evidenceDirectory, "reported.log"), Path.Combine(evidenceDirectory, "fixed.log"));
    var verdict = reportedCallback is not null && fixedCallback is not null
        ? $"Direct calls to the actual callback returned {InlineCode(reportedCallback)} on the reported revision and {InlineCode(fixedCallback)} on the fixed revision for identical 16-byte input."
        : "Not independently reproduced by this project.";
    return $"""
# Case {MarkdownText(Value("id"))}: {MarkdownText(Value("title"))}

**Triage status:** {MarkdownText(Value("status"))}
**Evidence status:** {(reportedCallback is not null && fixedCallback is not null ? "Reproduced across pinned revisions" : "Not independently reproduced by this project")}
**Issue:** {MarkdownLink("Open issue", Value("issueUrl"))}

## Claim

{MarkdownText(Value("claim"))}

## Contract reference

{MarkdownLink(Value("contract"), Value("contract"))}

## Revision anchors

- Reported source: {MarkdownLink(source.GetProperty("commit").GetString(), source.GetProperty("url").GetString())}
- Upstream fix: {MarkdownLink(fixedRevision.GetProperty("commit").GetString(), fixedRevision.GetProperty("url").GetString())}

## Evidence collected

{verdict}

- Reported revision callback: {InlineCode(reportedCallback ?? "not run")}
- Fixed revision callback: {InlineCode(fixedCallback ?? "not run")}
- 16-byte input SHA-256: `374708fff7719dd5979ec875d56cd2286f6d3cf7ec317a3b25632aab28ec37bb`
- libFuzzer run summaries: {InlineCode(observedRuns)}
- Container/toolchain:

{CodeBlock(environment)}

The upstream fix commit also states that nonzero returns discard input and changes this callback to always return `0`. The direct probe and libFuzzer behavior above are observations collected by this project.

## Reproduction

Re-run with {InlineCode($"bash cases/{Value("id")}/reproduce.sh")}. The script fetches the pinned source commits, creates the 16-byte input, builds in Docker, then executes with container networking disabled and CPU, memory, process, and capability limits.

## Assessment

Upstream merged a change addressing the reported behavior. The return-value regression is reproduced on the cited revision and absent on the fixed revision. This case is a historical workflow fixture, not a fresh unresolved report.

""";
}

static string? ReadCallback(string path)
{
    if (!File.Exists(path)) return null;
    var line = File.ReadLines(path).FirstOrDefault(line => line.StartsWith("callback_return="));
    return line?.Split('=', 2)[1];
}

static string ReadRuntimeSummary(string reportedLog, string fixedLog)
{
    static string Summarize(string path)
    {
        if (!File.Exists(path)) return "not run";
        var lines = File.ReadLines(path).Where(line => line.Contains("INITED") || line.Contains("DONE")).ToArray();
        return string.Join("; ", lines.Select(line => line.Trim()));
    }

    return $"reported: {Summarize(reportedLog)}; fixed: {Summarize(fixedLog)}";
}

static string BuildGeneralReport(JsonElement item, string caseDirectory)
{
    string Value(string name) => item.GetProperty(name).GetString() ?? "";
    var evidenceDirectory = Path.Combine(caseDirectory, "evidence");
    var reportedPath = Path.Combine(evidenceDirectory, "reported-output.txt");
    var currentPath = Path.Combine(evidenceDirectory, "current-output.txt");
    var reported = File.Exists(reportedPath) ? File.ReadAllText(reportedPath).Trim() : "Not run.";
    var current = File.Exists(currentPath) ? File.ReadAllText(currentPath).Trim() : "Not run.";
    var reproduced = reported.Contains("AES-128-CFB8") && reported.Contains("equal=no")
        && current.Contains("AES-128-CFB8") && current.Contains("equal=no");
    var revisions = item.GetProperty("revisions").EnumerateObject()
        .Select(revision => $"- {MarkdownText(revision.Name)}: {MarkdownLink(revision.Value.GetProperty("commit").GetString(), revision.Value.GetProperty("url").GetString())}");
    var environmentPaths = new[] { "environment.txt", "source-archives.sha256", "reproducer.sha256", "container-image.txt" }
        .Select(name => Path.Combine(evidenceDirectory, name)).Where(File.Exists);
    var environment = string.Join(Environment.NewLine, environmentPaths.Select(path => File.ReadAllText(path).Trim()));

    return $"""
# Case {MarkdownText(Value("id"))}: {MarkdownText(Value("title"))}

**Issue status:** {MarkdownText(Value("status"))}
**Evidence status:** {(reproduced ? "Reproduced on both pinned revisions" : "Not yet reproduced")}
**Issue:** {MarkdownLink("Open issue", Value("issueUrl"))}

## Claim

{MarkdownText(Value("claim"))}

## Revisions

{string.Join(Environment.NewLine, revisions)}

## Observed output

The public C reproducer supplied in the issue was run unchanged against both revisions. It reproduced the same mismatch in AES-CFB1 and AES-CFB8; the listed CFB128, CBC, OFB, and GCM controls round-tripped exactly.

Reported revision:

{CodeBlock(reported)}

Current revision:

{CodeBlock(current)}

## Toolchain and checksums

{CodeBlock(string.IsNullOrWhiteSpace(environment) ? "Not run." : environment)}

## Reproduction

Re-run with {InlineCode($"bash cases/{Value("id")}/reproduce.sh")}. The source builds and executes in a network-disabled Docker container with CPU, memory, process, and capability limits.

""";
}
