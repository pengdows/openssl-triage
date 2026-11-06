using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace OpenSslTriage.Cli.Tests;

public sealed class SecurityRegressionTests
{
    [Fact]
    public async Task Inspect_RejectsCaseIdThatEscapesCasesDirectory()
    {
        using var workspace = new TestWorkspace();
        var outside = Path.Combine(workspace.Root, "outside");
        Directory.CreateDirectory(outside);
        await File.WriteAllTextAsync(Path.Combine(outside, "case.json"), "{\"id\":\"secret\",\"title\":\"private data\"}");

        var result = await workspace.RunCliAsync("inspect", "../outside");

        Assert.NotEqual(0, result.ExitCode);
        Assert.DoesNotContain("private data", result.StdOut);
        Assert.Contains("case ID", result.StdErr, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Inspect_RejectsCaseDirectorySymlinkThatEscapesCasesDirectory()
    {
        using var workspace = new TestWorkspace();
        var outside = Path.Combine(workspace.Root, "outside");
        Directory.CreateDirectory(outside);
        await File.WriteAllTextAsync(Path.Combine(outside, "case.json"), "{\"id\":\"100\",\"title\":\"private data\"}");
        Directory.CreateSymbolicLink(Path.Combine(workspace.Root, "cases", "100"), outside);

        var result = await workspace.RunCliAsync("inspect", "100");

        Assert.NotEqual(0, result.ExitCode);
        Assert.DoesNotContain("private data", result.StdOut);
        Assert.Contains("inside the cases/ directory", result.StdErr);
    }

    [Fact]
    public async Task Inspect_RejectsCaseManifestSymlinkThatEscapesCasesDirectory()
    {
        using var workspace = new TestWorkspace();
        var outside = Path.Combine(workspace.Root, "outside.json");
        await File.WriteAllTextAsync(outside, "{\"id\":\"100\",\"title\":\"private data\"}");
        var caseDirectory = Path.Combine(workspace.Root, "cases", "100");
        Directory.CreateDirectory(caseDirectory);
        File.CreateSymbolicLink(Path.Combine(caseDirectory, "case.json"), outside);

        var result = await workspace.RunCliAsync("inspect", "100");

        Assert.NotEqual(0, result.ExitCode);
        Assert.DoesNotContain("private data", result.StdOut);
        Assert.Contains("inside the cases/ directory", result.StdErr);
    }

    [Fact]
    public async Task Inspect_ReportsUnknownNumericCaseId()
    {
        using var workspace = new TestWorkspace();

        var result = await workspace.RunCliAsync("inspect", "999");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Unknown case '999'", result.StdErr);
    }

    [Fact]
    public async Task Inspect_ReportsUnknownCaseWithoutManifest()
    {
        using var workspace = new TestWorkspace();
        Directory.CreateDirectory(Path.Combine(workspace.Root, "cases", "100"));

        var result = await workspace.RunCliAsync("inspect", "100");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("Unknown case '100'", result.StdErr);
    }

    [Fact]
    public async Task Inspect_WritesValidCaseAsJson()
    {
        using var workspace = new TestWorkspace();
        await workspace.WriteCaseAsync("100", "case title", "claim", "https://github.com/openssl/openssl/issues/100");

        var result = await workspace.RunCliAsync("inspect", "100");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("\"id\": \"100\"", result.StdOut);
        Assert.Contains("case title", result.StdOut);
    }

    [Fact]
    public async Task Cli_HelpDoesNotRequireCasesDirectory()
    {
        var temporaryDirectory = Path.Combine(Path.GetTempPath(), $"openssl-triage-help-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryDirectory);
        try
        {
            var result = await TestWorkspace.RunCliInDirectoryAsync(temporaryDirectory, "--help");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("Usage: openssl-triage", result.StdOut);
        }
        finally
        {
            Directory.Delete(temporaryDirectory);
        }
    }

    [Fact]
    public async Task Cli_RejectsUnknownCommand()
    {
        using var workspace = new TestWorkspace();

        var result = await workspace.RunCliAsync("unknown");

        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Usage: openssl-triage", result.StdErr);
    }

    [Fact]
    public async Task List_DoesNotReadNestedWorkManifestsOrEmitTerminalControls()
    {
        using var workspace = new TestWorkspace();
        await workspace.WriteCaseAsync("100", "safe\n\u001b[31mred", "claim", "https://github.com/openssl/openssl/issues/100");
        var nested = Path.Combine(workspace.Root, "cases", "100", "work", "download");
        Directory.CreateDirectory(nested);
        await File.WriteAllTextAsync(Path.Combine(nested, "case.json"), "{\"id\":\"200\",\"title\":\"nested private manifest\"}");
        var nonCase = Path.Combine(workspace.Root, "cases", "not-a-case");
        Directory.CreateDirectory(nonCase);
        await File.WriteAllTextAsync(Path.Combine(nonCase, "case.json"), "{\"id\":\"300\",\"title\":\"non-case manifest\"}");
        Directory.CreateDirectory(Path.Combine(workspace.Root, "cases", "101"));
        var outside = Path.Combine(workspace.Root, "outside");
        Directory.CreateDirectory(outside);
        await File.WriteAllTextAsync(Path.Combine(outside, "case.json"), "{\"id\":\"102\",\"title\":\"outside directory manifest\"}");
        Directory.CreateSymbolicLink(Path.Combine(workspace.Root, "cases", "102"), outside);
        var caseWithLinkedManifest = Path.Combine(workspace.Root, "cases", "103");
        Directory.CreateDirectory(caseWithLinkedManifest);
        File.CreateSymbolicLink(Path.Combine(caseWithLinkedManifest, "case.json"), Path.Combine(outside, "case.json"));

        var result = await workspace.RunCliAsync("list");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("100\\tsafe  [31mred", result.StdOut.Replace("\t", "\\t", StringComparison.Ordinal));
        Assert.DoesNotContain("nested private manifest", result.StdOut);
        Assert.DoesNotContain("non-case manifest", result.StdOut);
        Assert.DoesNotContain("outside directory manifest", result.StdOut);
        Assert.DoesNotContain('\u001b', result.StdOut);
        Assert.Contains("Skipping case directory outside cases/: 102", result.StdErr);
        Assert.Contains("Skipping case manifest outside cases/: 103", result.StdErr);
        Assert.Single(result.StdOut.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public async Task Cli_ReportsUsageWithoutCasesDirectory()
    {
        var emptyDirectory = Path.Combine(Path.GetTempPath(), $"openssl-triage-empty-{Guid.NewGuid():N}");
        Directory.CreateDirectory(emptyDirectory);
        try
        {
            var result = await TestWorkspace.RunCliInDirectoryAsync(emptyDirectory, "inspect", "100");

            Assert.Equal(2, result.ExitCode);
            Assert.Contains("Could not find a cases/ directory", result.StdErr);
        }
        finally
        {
            Directory.Delete(emptyDirectory);
        }
    }

    [Fact]
    public async Task Inspect_RejectsManifestWhoseIdDoesNotMatchItsDirectory()
    {
        using var workspace = new TestWorkspace();
        var caseDirectory = Path.Combine(workspace.Root, "cases", "100");
        Directory.CreateDirectory(caseDirectory);
        await File.WriteAllTextAsync(Path.Combine(caseDirectory, "case.json"), "{\"id\":\"101\",\"title\":\"mismatch\"}");

        var result = await workspace.RunCliAsync("inspect", "100");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("does not match", result.StdErr);
    }

    [Fact]
    public async Task Report_EscapesUntrustedManifestTextAndRejectsActiveUrlSchemes()
    {
        using var workspace = new TestWorkspace();
        await workspace.WriteCaseAsync("100", "<script>alert(1)</script> [click](https://evil.example)",
            "![x](javascript:alert(2)) @all", "javascript:alert(3)");

        var result = await workspace.RunCliAsync("report", "100");
        var report = await File.ReadAllTextAsync(Path.Combine(workspace.Root, "cases", "100", "evidence-report.md"));

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("<script>", report);
        Assert.DoesNotContain("[click](https://evil.example)", report);
        Assert.DoesNotContain("](javascript:", report);
        Assert.Contains("\\[click\\]", report);
        Assert.Contains("\\@all", report);
    }

    [Fact]
    public async Task Report_UsesFenceThatCannotBeClosedByEvidenceText()
    {
        using var workspace = new TestWorkspace();
        await workspace.WriteCaseAsync("101", "title", "claim", "https://github.com/openssl/openssl/issues/101");
        var evidence = Path.Combine(workspace.Root, "cases", "101", "evidence");
        Directory.CreateDirectory(evidence);
        await File.WriteAllTextAsync(Path.Combine(evidence, "reported-output.txt"), "first line\n````\nforged heading\n");

        var result = await workspace.RunCliAsync("report", "101");
        var report = await File.ReadAllTextAsync(Path.Combine(workspace.Root, "cases", "101", "evidence-report.md"));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("`````text", report);
    }

    [Fact]
    public async Task Report_PreservesHttpsLinksForTrustedSyntax()
    {
        using var workspace = new TestWorkspace();
        await workspace.WriteCaseAsync("102", "title", "claim", "https://github.com/openssl/openssl/issues/102");

        var result = await workspace.RunCliAsync("report", "102");
        var report = await File.ReadAllTextAsync(Path.Combine(workspace.Root, "cases", "102", "evidence-report.md"));

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("[Open issue](<https://github.com/openssl/openssl/issues/102>)", report);
    }

    [Fact]
    public async Task Report_DoesNotCreateLinkWithEmbeddedCredentials()
    {
        using var workspace = new TestWorkspace();
        await workspace.WriteCaseAsync("103", "title", "claim", "https://user:password@evil.example/issues/103");

        var result = await workspace.RunCliAsync("report", "103");
        var report = await File.ReadAllTextAsync(Path.Combine(workspace.Root, "cases", "103", "evidence-report.md"));

        Assert.Equal(0, result.ExitCode);
        Assert.DoesNotContain("]( <https://", report);
        Assert.DoesNotContain("](<https://user:password@", report);
    }

    [Fact]
    public async Task Report_SanitizesControlCharactersInOutputPathMessage()
    {
        using var workspace = new TestWorkspace();
        await workspace.WriteCaseAsync("104", "title", "claim", "https://github.com/openssl/openssl/issues/104");
        var output = Path.Combine(workspace.Root, "report-\u001b[31m.md");

        var result = await workspace.RunCliAsync("report", "104", "--output", output);

        Assert.Equal(0, result.ExitCode);
        Assert.True(File.Exists(output));
        Assert.DoesNotContain('\u001b', result.StdOut);
    }

    [Fact]
    public async Task HistoricalReport_ContainsCallbackEvidenceFromPinnedFixture()
    {
        using var workspace = new TestWorkspace();
        var output = Path.Combine(workspace.Root, "historical-report.md");

        var result = await TestWorkspace.RunCliInDirectoryAsync(FindRepositoryRoot(), "report", "32897", "--output", output);
        var report = await File.ReadAllTextAsync(output);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Case 32897", report);
        Assert.Contains("Direct calls to the actual callback", report);
        Assert.Contains("libFuzzer run summaries", report);
    }

    [Theory]
    [InlineData("32897", "fuzzer-return-probe.c")]
    [InlineData("33450", "ec-group-cmp-nullgen.c")]
    [InlineData("33450", "ec_group_cmp.patch")]
    [InlineData("33537", "cms_cfb.c")]
    public void DockerIgnore_AllowListsDockerfileInputsOnly(string caseId, string requiredInput)
    {
        var repoRoot = FindRepositoryRoot();
        var caseDirectory = Path.Combine(repoRoot, "cases", caseId);
        var ignored = File.ReadAllLines(Path.Combine(caseDirectory, ".dockerignore"))
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToArray();

        Assert.Contains("*", ignored);
        Assert.Contains("!Dockerfile", ignored);
        Assert.Contains("!run-in-container.sh", ignored);
        Assert.Contains($"!{requiredInput}", ignored);
        Assert.DoesNotContain("!evidence", ignored);
        Assert.DoesNotContain("!work", ignored);
        Assert.DoesNotContain("!.env", ignored);
        Assert.DoesNotContain("!cases", ignored);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "src", "OpenSslTriage.Cli", "OpenSslTriage.Cli.csproj")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not find the OpenSSL Triage repository root.");
    }

    private sealed class TestWorkspace : IDisposable
    {
        public TestWorkspace()
        {
            Root = Path.Combine(Path.GetTempPath(), $"openssl-triage-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path.Combine(Root, "cases"));
        }

        public string Root { get; }

        public async Task WriteCaseAsync(string id, string title, string claim, string issueUrl)
        {
            var caseDirectory = Path.Combine(Root, "cases", id);
            Directory.CreateDirectory(caseDirectory);
            var manifest = new
            {
                id,
                title,
                status = "open",
                issueUrl,
                claim,
                revisions = new
                {
                    current = new
                    {
                        commit = "abc123",
                        url = issueUrl
                    }
                }
            };
            await File.WriteAllTextAsync(Path.Combine(caseDirectory, "case.json"), JsonSerializer.Serialize(manifest));
        }

        public async Task<CliResult> RunCliAsync(params string[] arguments)
        {
            return await RunCliInDirectoryAsync(Root, arguments);
        }

        public static async Task<CliResult> RunCliInDirectoryAsync(string workingDirectory, params string[] arguments)
        {
            var cliAssembly = Path.Combine(AppContext.BaseDirectory, "OpenSslTriage.Cli.dll");
            var startInfo = new ProcessStartInfo("dotnet")
            {
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.ArgumentList.Add(cliAssembly);
            foreach (var argument in arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }

            using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start dotnet CLI process.");
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            return new CliResult(process.ExitCode, await stdoutTask, await stderrTask);
        }

        public void Dispose()
        {
            Directory.Delete(Root, recursive: true);
        }
    }

    private sealed record CliResult(int ExitCode, string StdOut, string StdErr);
}
