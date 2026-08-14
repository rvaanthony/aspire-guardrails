#if (UsePostgres)
using Testcontainers.PostgreSql;

namespace Template.Data.Tests.Postgres;

public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:16-alpine")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgresCollection : ICollectionFixture<PostgresContainerFixture>
{
    public const string Name = "Postgres";
}
#endif
