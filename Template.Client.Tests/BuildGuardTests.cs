using System.Diagnostics;

namespace Template.Client.Tests;

/// <summary>
/// Tests the build-time guardrails in <c>Directory.Build.targets</c> by making each
/// one fail on purpose.
/// <para>
/// These exist because a guardrail nobody has watched fail is decoration. The layer
/// map is one of this solution's load-bearing claims, and until something planted a
/// violation and asserted the build rejected it, "the layering is enforced" rested
/// on the target having been correct the day it was written. An edit that silently
/// stopped it firing — a mistyped item name, a condition that no longer matches —
/// would leave every other test green.
/// </para>
/// <para>
/// Each case synthesises a throwaway project that imports the real
/// <c>Directory.Build.props</c> and <c>Directory.Build.targets</c>, then invokes the
/// validation target directly. Nothing is compiled and nothing is restored, so a
/// case costs a second or two. The referenced projects do not need to exist: the
/// target reads the file NAME out of each ProjectReference, which is what makes this
/// self-contained.
/// </para>
/// </summary>
public sealed class BuildGuardTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [Fact]
    public void LegalReferenceToALowerRankPasses()
    {
        // The control. Without it, a target that failed on everything would look
        // like a target that worked.
        var result = RunGuard(
            projectName: "Template.Client",
            target: "ValidateLayering",
            projectReferences: ["Template.Business"]);

        Assert.True(result.ExitCode == 0, $"Expected success, got:\n{result.Output}");
    }

    [Fact]
    public void ReferenceToTheSameRankFailsWithArch0002()
    {
        // Client and Worker are both rank 90. This is the case the rank model exists
        // for and the one an exact-reference-list test cannot express: two hosts
        // quietly sharing a helper instead of moving it down a layer.
        var result = RunGuard(
            projectName: "Template.Client",
            target: "ValidateLayering",
            projectReferences: ["Template.Worker"]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("ARCH0002", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void ReferenceToAHigherRankFailsWithArch0002()
    {
        var result = RunGuard(
            projectName: "Template.Business",
            target: "ValidateLayering",
            projectReferences: ["Template.Client"]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("ARCH0002", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void ProjectMissingFromTheLayerMapFailsWithArch0001()
    {
        var result = RunGuard(
            projectName: "Template.NotInTheMap",
            target: "ValidateLayering",
            projectReferences: ["Template.Common"]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("ARCH0001", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void ReferenceMissingFromTheLayerMapFailsWithArch0001()
    {
        var result = RunGuard(
            projectName: "Template.Client",
            target: "ValidateLayering",
            projectReferences: ["Unrelated.Package"]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("ARCH0001", result.Output, StringComparison.Ordinal);
    }

    [Fact]
    public void InvariantGlobalizationFailsWithArch0003()
    {
        // Silently fatal to Microsoft.Data.SqlClient, which throws before it opens a
        // socket. There is no build or restore warning for it, so this target is the
        // only thing standing between that property and a deployment where every
        // database call fails.
        var result = RunGuard(
            projectName: "Template.Client",
            target: "ValidateGlobalization",
            projectReferences: [],
            properties: new Dictionary<string, string> { ["InvariantGlobalization"] = "true" });

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("ARCH0003", result.Output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("ValidateLayering", "EnforceLayering")]
    [InlineData("ValidateGlobalization", "EnforceGlobalization")]
    public void EachGuardHasAWorkingEscapeHatch(string target, string switchName)
    {
        // Both guards document an opt-out for the justified case. An opt-out that
        // does not work is worse than none: it sends whoever needs it looking for a
        // different way around the rule.
        var properties = new Dictionary<string, string>
        {
            [switchName] = "false",
            ["InvariantGlobalization"] = "true"
        };

        var result = RunGuard(
            projectName: "Template.Client",
            target: target,
            projectReferences: ["Template.Worker"],
            properties: properties);

        Assert.True(result.ExitCode == 0, $"Expected the escape hatch to allow it, got:\n{result.Output}");
    }

    private static (int ExitCode, string Output) RunGuard(
        string projectName,
        string target,
        string[] projectReferences,
        Dictionary<string, string>? properties = null)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"guard-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);

        try
        {
            // No Sdk attribute, so MSBuild performs no implicit imports and the two
            // files under test are the only influence on the result.
            var references = string.Concat(projectReferences.Select(name =>
                $"""<ProjectReference Include="..\{name}\{name}.csproj" />"""));
            var propertyElements = string.Concat((properties ?? []).Select(pair =>
                $"<{pair.Key}>{pair.Value}</{pair.Key}>"));

            var projectPath = Path.Combine(directory, $"{projectName}.proj");
            File.WriteAllText(projectPath, $"""
                <Project>
                  <Import Project="{Path.Combine(RepositoryRoot, "Directory.Build.props")}" />
                  <PropertyGroup>{propertyElements}</PropertyGroup>
                  <ItemGroup>{references}</ItemGroup>
                  <Import Project="{Path.Combine(RepositoryRoot, "Directory.Build.targets")}" />
                </Project>
                """);

            using var process = new Process
            {
                StartInfo = new ProcessStartInfo("dotnet")
                {
                    ArgumentList = { "msbuild", projectPath, $"-t:{target}", "-nologo" },
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    WorkingDirectory = directory
                }
            };

            process.Start();
            var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();

            if (!process.WaitForExit(TimeSpan.FromMinutes(2)))
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException($"'dotnet msbuild -t:{target}' did not finish within two minutes.");
            }

            return (process.ExitCode, output);
        }
        finally
        {
            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch (IOException)
            {
                // A leftover temp directory is not worth failing a test over.
            }
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Template.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}
