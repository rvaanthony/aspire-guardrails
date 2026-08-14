#if (!UseEfCore)
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Template.Data.Storage;
using Template.Data.Tests.Azurite;

namespace Template.Data.Tests.Storage;

[Collection(AzuriteCollection.Name)]
public sealed class StorageInitializerHostedServiceTests
{
    private readonly AzuriteContainerFixture _fixture;

    public StorageInitializerHostedServiceTests(AzuriteContainerFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task StartAsync_CreatesTableContainerAndQueue()
    {
        var options = new StorageOptions();
        var service = CreateService(options);

        await service.StartAsync(CancellationToken.None);

        Assert.True(await TableExistsAsync(options.ContactSubmissionsTableName));

        var blobs = new BlobServiceClient(_fixture.ConnectionString);
        var containerExists = await blobs.GetBlobContainerClient(options.PrivateContainerName).ExistsAsync();
        Assert.True(containerExists.Value);

        var queues = new QueueServiceClient(_fixture.ConnectionString);
        var queueExists = await queues.GetQueueClient(options.JobsQueueName).ExistsAsync();
        Assert.True(queueExists.Value);
    }

    [Fact]
    public async Task StartAsync_IsIdempotent()
    {
        var options = new StorageOptions();
        var service = CreateService(options);

        await service.StartAsync(CancellationToken.None);
        await service.StartAsync(CancellationToken.None);

        Assert.True(await TableExistsAsync(options.ContactSubmissionsTableName));
    }

    private StorageInitializerHostedService CreateService(StorageOptions options) =>
        new(
            new TableServiceClient(_fixture.ConnectionString),
            new BlobServiceClient(_fixture.ConnectionString),
            new QueueServiceClient(_fixture.ConnectionString),
            Options.Create(options),
            NullLogger<StorageInitializerHostedService>.Instance);

    private async Task<bool> TableExistsAsync(string tableName)
    {
        var tables = new TableServiceClient(_fixture.ConnectionString);
        await foreach (var table in tables.QueryAsync(t => t.Name == tableName))
        {
            return true;
        }

        return false;
    }
}
#endif
