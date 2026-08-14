#if (UseSqlServer)
using Testcontainers.MsSql;

namespace Template.Data.Tests.SqlServer;

public sealed class SqlServerContainerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SqlServerCollection : ICollectionFixture<SqlServerContainerFixture>
{
    public const string Name = "SqlServer";
}
#endif
