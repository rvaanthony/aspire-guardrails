using System.Xml.Linq;

namespace Template.Client.Tests;

public sealed class ArchitectureTests
{
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    // New projects must be added here; an omitted project is silently
    // unchecked, not flagged — AllProjectsAreCoveredByArchitectureTest guards
    // against that by asserting every */*.csproj appears in this map. A
    // project that doesn't exist for the current stamp (e.g. an optional
    // Template.Worker) is simply absent from disk, so AllowedDependencies
    // below skips it rather than failing.
    private static readonly (string ProjectName, string[] Allowed)[] CandidateDependencies =
    [
        ("Template.Common", []),
        ("Template.ServiceDefaults", []),
        ("Template.Business", ["Template.Common"]),
        ("Template.Data", ["Template.Business", "Template.Common"]),
        ("Template.Client", ["Template.Business", "Template.Common", "Template.Data", "Template.ServiceDefaults"]),
        ("Template.AppHost", ["Template.Client"]),
        ("Template.Worker", ["Template.Business", "Template.Common", "Template.Data", "Template.ServiceDefaults"]),
        ("Template.AppHost.Tests", ["Template.AppHost"]),
        ("Template.Business.Tests", ["Template.Business"]),
        ("Template.Data.Tests", ["Template.Data"]),
        ("Template.Client.Tests", ["Template.Client"]),
        ("Template.Worker.Tests", ["Template.Worker"])
    ];

    public static TheoryData<string, string[]> AllowedDependencies
    {
        get
        {
            var data = new TheoryData<string, string[]>();
            // AppHost orchestrates whatever's present, so it's the one project whose
            // allow-list legitimately depends on another optional project's existence.
            var workerExists = File.Exists(Path.Combine(RepositoryRoot, "Template.Worker", "Template.Worker.csproj"));

            foreach (var (projectName, allowed) in CandidateDependencies)
            {
                if (!File.Exists(Path.Combine(RepositoryRoot, projectName, $"{projectName}.csproj")))
                {
                    continue;
                }

                var effectiveAllowed = projectName == "Template.AppHost" && workerExists
                    ? [.. allowed, "Template.Worker"]
                    : allowed;

                data.Add(projectName, effectiveAllowed);
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(AllowedDependencies))]
    public void CoreLayersOnlyReferenceAllowedProjects(string projectName, string[] allowedDependencies)
    {
        var projectPath = Path.Combine(RepositoryRoot, projectName, $"{projectName}.csproj");
        var document = XDocument.Load(projectPath);
        var actualDependencies = document
            .Descendants()
            .Where(element => element.Name.LocalName == "ProjectReference")
            .Select(element => element.Attribute("Include")?.Value)
            .OfType<string>()
            // csproj Include paths are always backslash-separated regardless of OS;
            // normalize before extracting the filename so this also works on the
            // Linux CI runner, where Path treats '\' as an ordinary character.
            .Select(include => Path.GetFileNameWithoutExtension(include.Replace('\\', '/')))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(allowedDependencies.Order(StringComparer.Ordinal), actualDependencies);
    }

    [Fact]
    public void AllProjectsAreCoveredByArchitectureTest()
    {
        var coveredProjectNames = CandidateDependencies
            .Select(x => x.ProjectName)
            .ToHashSet(StringComparer.Ordinal);

        var allProjectNames = Directory.GetDirectories(RepositoryRoot)
            .SelectMany(dir => Directory.GetFiles(dir, "*.csproj"))
            .Select(Path.GetFileNameWithoutExtension)
            .OfType<string>()
            .Order(StringComparer.Ordinal)
            .ToArray();

        var uncovered = allProjectNames.Where(name => !coveredProjectNames.Contains(name)).ToArray();
        Assert.Empty(uncovered);
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
