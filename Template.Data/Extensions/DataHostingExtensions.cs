using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Template.Common.Interfaces;
// The conditional using sits BEFORE Template.Data.Stores so that the list is
// still alphabetically sorted after either branch resolves: Persistence and
// Storage both sort before Stores. Put it after, and `dotnet format` in the
// generated app reports IMPORTS on a file nobody has touched — while this
// repository stays green, because format does not reorder across a preprocessor
// directive.
#if (UseEfCore)
using Template.Data.Persistence;
#else
using Template.Data.Storage;
#endif
using Template.Data.Stores;

namespace Template.Data.Extensions;

public static class DataHostingExtensions
{
    public static TBuilder AddDataInfrastructure<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Fast HTTP-boundary tests exercise the web app without Docker —
        // they can replace this with a spy/fake via ConfigureServices.
        if (builder.Environment.IsEnvironment("Testing"))
        {
            builder.Services.AddSingleton<IContactSubmissionStore, InMemoryContactSubmissionStore>();
            return builder;
        }

#if (UsePostgres)
        builder.AddNpgsqlDbContext<AppDbContext>("template");
        builder.Services.AddHostedService<DatabaseInitializerHostedService>();
        builder.Services.AddScoped<IContactSubmissionStore, EfContactSubmissionStore>();
#elif (UseSqlServer)
        builder.AddSqlServerDbContext<AppDbContext>("template");
        builder.Services.AddHostedService<DatabaseInitializerHostedService>();
        builder.Services.AddScoped<IContactSubmissionStore, EfContactSubmissionStore>();
#else
        builder.Services
            .AddOptions<StorageOptions>()
            .BindConfiguration(StorageOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        builder.AddAzureTableServiceClient(StorageConnectionNames.Tables);
        builder.AddAzureBlobServiceClient(StorageConnectionNames.Blobs);
        builder.AddAzureQueueServiceClient(StorageConnectionNames.Queues);
        builder.Services.AddHostedService<StorageInitializerHostedService>();
        builder.Services.AddScoped<IContactSubmissionStore, ContactSubmissionStore>();
#endif

        return builder;
    }
}
