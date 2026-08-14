#if (!UseEfCore)
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Queues;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Template.Data.Storage;

/// <summary>
/// Ensures the contact-submissions table, private blob container, and jobs queue
/// exist at application startup. Idempotent create-if-not-exists calls.
/// </summary>
public sealed class StorageInitializerHostedService : IHostedService
{
    private readonly TableServiceClient _tables;
    private readonly BlobServiceClient _blobs;
    private readonly QueueServiceClient _queues;
    private readonly StorageOptions _options;
    private readonly ILogger<StorageInitializerHostedService> _logger;

    public StorageInitializerHostedService(
        TableServiceClient tables,
        BlobServiceClient blobs,
        QueueServiceClient queues,
        IOptions<StorageOptions> options,
        ILogger<StorageInitializerHostedService> logger)
    {
        _tables = tables;
        _blobs = blobs;
        _queues = queues;
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _tables.CreateTableIfNotExistsAsync(_options.ContactSubmissionsTableName, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Table {TableName} ensured.", _options.ContactSubmissionsTableName);

        var container = _blobs.GetBlobContainerClient(_options.PrivateContainerName);
        await container.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Blob container {ContainerName} ensured.", _options.PrivateContainerName);

        var queue = _queues.GetQueueClient(_options.JobsQueueName);
        await queue.CreateIfNotExistsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Queue {QueueName} ensured.", _options.JobsQueueName);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
#endif
