using Microsoft.Extensions.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

#if (UsePostgres)
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume();
var database = postgres.AddDatabase("template");

builder.AddProject<Projects.Template_Client>("template-client")
    .WithReference(database)
    .WaitFor(database)
    .WithHttpHealthCheck("/health");
#if (UseWorker)

builder.AddProject<Projects.Template_Worker>("template-worker")
    .WithReference(database)
    .WaitFor(database);
#endif
#elif (UseSqlServer)
var sql = builder.AddSqlServer("sql")
    .WithDataVolume();
var database = sql.AddDatabase("template");

builder.AddProject<Projects.Template_Client>("template-client")
    .WithReference(database)
    .WaitFor(database)
    .WithHttpHealthCheck("/health");
#if (UseWorker)

builder.AddProject<Projects.Template_Worker>("template-worker")
    .WithReference(database)
    .WaitFor(database);
#endif
#else
var storage = builder.AddAzureStorage("storage");
if (builder.Environment.IsDevelopment())
{
    // RunAsEmulator already passes --skipApiVersionCheck to Azurite by default, which
    // is required because the Azure.Storage SDK sends a newer service API version than
    // Azurite understands. Do NOT call WithApiVersionCheck(false) here: despite the name,
    // it *removes* that default flag and re-enables the check, causing InvalidHeaderValue.
    // Explicit host ports, generated per created project. Aspire otherwise defaults
    // to Azurite's well-known 10000-10002, and if anything already holds those — the
    // Azurite VS Code extension, Storage Explorer, another project — startup emits
    // three "unable to allocate a network port" warnings and then hangs, which is a
    // hard failure wearing a warning's clothes.
    storage.RunAsEmulator(emulator => emulator
        .WithBlobPort(10810)
        .WithQueuePort(10811)
        .WithTablePort(10812));
}

var blobs = storage.AddBlobs("blobs");
var queues = storage.AddQueues("queues");
var tables = storage.AddTables("tables");

builder.AddProject<Projects.Template_Client>("template-client")
    .WithReference(blobs)
    .WithReference(queues)
    .WithReference(tables)
    .WithHttpHealthCheck("/health");
#if (UseWorker)

builder.AddProject<Projects.Template_Worker>("template-worker")
    .WithReference(blobs)
    .WithReference(queues)
    .WithReference(tables);
#endif
#endif

builder.Build().Run();
