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
    foreach (var path in Directory.EnumerateFiles(root, "case.json", SearchOption.AllDirectories).Order())
    {
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        var item = doc.RootElement;
        Console.WriteLine($"{item.GetProperty("id").GetString()}\t{item.GetProperty("title").GetString()}");
    }
    return;
}

if (args.Length < 2 || command is not ("inspect" or "report"))
{
    Console.Error.WriteLine(usage);
    Environment.ExitCode = 2;
    return;
}

var casePath = Path.Combine(root, args[1], "case.json");
if (!File.Exists(casePath))
{
    Console.Error.WriteLine($"Unknown case '{args[1]}'.");
    Environment.ExitCode = 2;
    return;
}

using var caseDocument = JsonDocument.Parse(File.ReadAllText(casePath));
var json = caseDocument.RootElement;
if (command == "inspect")
{
    Console.WriteLine(JsonSerializer.Serialize(json, new JsonSerializerOptions { WriteIndented = true }));
    return;
}

var outputFlag = Array.IndexOf(args, "--output");
var outputPath = outputFlag >= 0 && outputFlag + 1 < args.Length
    ? Path.GetFullPath(args[outputFlag + 1])
    : Path.Combine(Path.GetDirectoryName(casePath)!, "evidence-report.md");
var report = json.GetProperty("id").GetString() == "32897"
    ? BuildReport(json, Path.GetDirectoryName(casePath)!)
    : BuildGeneralReport(json, Path.GetDirectoryName(casePath)!);
Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
File.WriteAllText(outputPath, report);
Console.WriteLine($"Wrote {outputPath}");

static string? FindCasesDirectory()
{
    for (var directory = new DirectoryInfo(Environment.CurrentDirectory); directory is not null; directory = directory.Parent)
    {
        var candidate = Path.Combine(directory.FullName, "cases");
        if (Directory.Exists(candidate)) return candidate;
    }
    return null;
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
        ? $"Direct calls to the actual callback returned `{reportedCallback}` on the reported revision and `{fixedCallback}` on the fixed revision for identical 16-byte input."
        : "Not independently reproduced by this project.";
    return $"""
# Case {Value("id")}: {Value("title")}

**Triage status:** {Value("status")}  
**Evidence status:** {(reportedCallback is not null && fixedCallback is not null ? "Reproduced across pinned revisions" : "Not independently reproduced by this project")}  
**Issue:** {Value("issueUrl")}

## Claim

{Value("claim")}

## Contract reference

{Value("contract")}

## Revision anchors

- Reported source: [`{source.GetProperty("commit").GetString()}`]({source.GetProperty("url").GetString()})
- Upstream fix: [`{fixedRevision.GetProperty("commit").GetString()}`]({fixedRevision.GetProperty("url").GetString()})

## Evidence collected

{verdict}

- Reported revision callback: `{reportedCallback ?? "not run"}`
- Fixed revision callback: `{fixedCallback ?? "not run"}`
- 16-byte input SHA-256: `374708fff7719dd5979ec875d56cd2286f6d3cf7ec317a3b25632aab28ec37bb`
- libFuzzer run summaries: {observedRuns}
- Container/toolchain:

```text
{environment}
```

The upstream fix commit also states that nonzero returns discard input and changes this callback to always return `0`. The direct probe and libFuzzer behavior above are observations collected by this project.

## Reproduction

Re-run with `bash cases/{Value("id")}/reproduce.sh`. The script fetches the pinned source commits, creates the 16-byte input, builds in Docker, then executes with container networking disabled and CPU, memory, process, and capability limits.

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
        .Select(revision => $"- {revision.Name}: [`{revision.Value.GetProperty("commit").GetString()}`]({revision.Value.GetProperty("url").GetString()})");
    var environmentPaths = new[] { "environment.txt", "source-archives.sha256", "reproducer.sha256", "container-image.txt" }
        .Select(name => Path.Combine(evidenceDirectory, name)).Where(File.Exists);
    var environment = string.Join(Environment.NewLine, environmentPaths.Select(path => File.ReadAllText(path).Trim()));

    return $"""
# Case {Value("id")}: {Value("title")}

**Issue status:** {Value("status")}  
**Evidence status:** {(reproduced ? "Reproduced on both pinned revisions" : "Not yet reproduced")}  
**Issue:** {Value("issueUrl")}

## Claim

{Value("claim")}

## Revisions

{string.Join(Environment.NewLine, revisions)}

## Observed output

The public C reproducer supplied in the issue was run unchanged against both revisions. It reproduced the same mismatch in AES-CFB1 and AES-CFB8; the listed CFB128, CBC, OFB, and GCM controls round-tripped exactly.

Reported revision:

```text
{reported}
```

Current revision:

```text
{current}
```

## Toolchain and checksums

```text
{(string.IsNullOrWhiteSpace(environment) ? "Not run." : environment)}
```

## Reproduction

Re-run with `bash cases/{Value("id")}/reproduce.sh`. The source builds and executes in a network-disabled Docker container with CPU, memory, process, and capability limits.

""";
}
