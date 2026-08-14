using Aspire.Hosting.ApplicationModel;

namespace Template.AppHost.Tests;

/// <summary>
/// Asserts the AppHost wires every host to every backing resource it needs.
/// <para>
/// This is the gap the other suites structurally cannot cover. Business and Data
/// tests fake or create their own storage; Client tests run under the
/// <c>Testing</c> environment, which skips real storage registration entirely. So
/// nothing else notices when a resource is added to one host and forgotten on
/// another, or when a connection name is renamed on one side only — both of which
/// build clean and fail at startup in a deployed environment.
/// </para>
/// </summary>
public sealed class AppHostWiringTests : IClassFixture<AppHostFixture>
{
    private readonly AppHostFixture _fixture;

    public AppHostWiringTests(AppHostFixture fixture) => _fixture = fixture;

    /// <summary>
    /// The resource names every host must reference. These are the exact strings
    /// Template.Data resolves its connections by, so a rename on one side and not
    /// the other produces a missing-connection-string failure at startup and no
    /// other signal.
    /// </summary>
    private static readonly string[] ExpectedBackingResources =
#if (UseEfCore)
        ["template"];
#else
        ["blobs", "queues", "tables"];
#endif

    [Fact]
    public void ClientReferencesEveryBackingResource() =>
        AssertReferences("template-client");

#if (UseWorker)
    [Fact]
    public void WorkerReferencesEveryBackingResource() =>
        AssertReferences("template-worker");

    [Fact]
    public void ClientAndWorkerReferenceTheSameResources()
    {
        // The drift this catches: a resource gets added to the Client and not to
        // the Worker (or the reverse). Both hosts share Template.Data, so the one
        // that was missed throws on its first store call — in the background, where
        // nobody is watching.
        Assert.Equal(ReferencedResourceNames("template-client"), ReferencedResourceNames("template-worker"));
    }
#endif

    [Fact]
    public void ClientHasAnHttpHealthCheck()
    {
        // WithHttpHealthCheck on the Client is load-bearing beyond Aspire's
        // dashboard: the deploy workflow polls /health to decide whether a release
        // came up. Dropping it leaves both the orchestrator and the deploy gate
        // with nothing to wait on.
        var client = Resource("template-client");
        Assert.NotEmpty(client.Annotations.OfType<HealthCheckAnnotation>());
    }
#if (!UseEfCore)

    [Fact]
    public void StorageRunsAsAnEmulatorInDevelopment()
    {
        // Guards against a local run reaching for real Azure Storage — which fails
        // with an authentication error if you are lucky, and writes to a real
        // account if you are not.
        var storage = Resource("storage");
        Assert.NotEmpty(storage.Annotations.OfType<EmulatorResourceAnnotation>());
    }

    [Fact]
    public void StorageEmulatorDoesNotUseAzuritesDefaultPorts()
    {
        // Aspire defaults the emulator's host ports to Azurite's well-known
        // 10000-10002. Anything already holding those — the Azurite VS Code
        // extension, Storage Explorer, another app — makes `dotnet run` on the
        // AppHost emit three "unable to allocate a network port" warnings and then
        // hang: a hard failure wearing a warning's clothes, with nothing in the
        // output naming the cause.
        //
        // AppHost.cs therefore sets them explicitly, from ports generated per
        // created project. This asserts that has not been undone, because the
        // symptom is a hang on someone else's machine rather than a failing test.
        var storage = Resource("storage");
        var endpoints = storage.Annotations.OfType<EndpointAnnotation>().ToArray();

        Assert.NotEmpty(endpoints);
        Assert.All(endpoints, endpoint =>
        {
            Assert.NotNull(endpoint.Port);
            Assert.DoesNotContain(endpoint.Port!.Value, new[] { 10000, 10001, 10002 });
        });

        // Distinct, or two of the three services would contend for one port.
        var ports = endpoints.Select(endpoint => endpoint.Port!.Value).ToArray();
        Assert.Equal(ports.Length, ports.Distinct().Count());
    }
#endif

    private void AssertReferences(string resourceName) =>
        Assert.Equal(
            ExpectedBackingResources.Order(StringComparer.Ordinal),
            ReferencedResourceNames(resourceName));

    private string[] ReferencedResourceNames(string resourceName) =>
        [.. Resource(resourceName).Annotations
            .OfType<ResourceRelationshipAnnotation>()
            .Where(relationship => relationship.Type == "Reference")
            .Select(relationship => relationship.Resource.Name)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

    private IResource Resource(string resourceName)
    {
        var resource = _fixture.Builder.Resources
            .SingleOrDefault(candidate => string.Equals(candidate.Name, resourceName, StringComparison.Ordinal));

        Assert.NotNull(resource);
        return resource;
    }
}
