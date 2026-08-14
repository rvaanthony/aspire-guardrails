using Aspire.Hosting.Testing;

namespace Template.AppHost.Tests;

/// <summary>
/// Builds the AppHost's resource graph once per test class, in process: nothing calls
/// <c>BuildAsync</c> or <c>StartAsync</c>, so no container starts and no port is
/// allocated.
/// <para>
/// "--environment Development" is not incidental. AppHost.cs takes a different branch
/// under <c>IsDevelopment()</c> — the storage emulator, data volumes — and
/// <see cref="DistributedApplicationTestingBuilder"/> does not default to Development,
/// so without it these tests would assert over a graph no developer ever runs.
/// </para>
/// </summary>
public sealed class AppHostFixture : IAsyncLifetime
{
    private IDistributedApplicationTestingBuilder? _builder;

    public IDistributedApplicationTestingBuilder Builder =>
        _builder ?? throw new InvalidOperationException("InitializeAsync has not run.");

    public async Task InitializeAsync() =>
        _builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.Template_AppHost>(["--environment", "Development"]);

    public Task DisposeAsync() => Task.CompletedTask;
}
