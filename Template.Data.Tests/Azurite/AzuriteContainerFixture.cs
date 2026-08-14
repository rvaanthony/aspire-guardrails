#if (!UseEfCore)
using Testcontainers.Azurite;

namespace Template.Data.Tests.Azurite;

public sealed class AzuriteContainerFixture : IAsyncLifetime
{
    private readonly AzuriteContainer _container = new AzuriteBuilder(
            "mcr.microsoft.com/azure-storage/azurite:3.35.0")
        .WithCommand("--skipApiVersionCheck")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AzuriteCollection : ICollectionFixture<AzuriteContainerFixture>
{
    public const string Name = "Azurite";
}
#endif
