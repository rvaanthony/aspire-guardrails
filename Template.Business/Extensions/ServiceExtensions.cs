using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Template.Business.Services;

namespace Template.Business.Extensions;

public static class ServiceExtensions
{
    public static TBuilder AddBusinessServices<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Register options with fail-fast validation so a misconfigured deploy
        // dies at startup instead of at first use:
        //
        //   builder.Services
        //       .AddOptions<MyFeatureOptions>()
        //       .BindConfiguration(MyFeatureOptions.SectionName)
        //       .ValidateDataAnnotations()
        //       .ValidateOnStart();

        builder.Services.AddScoped<ContactIntakeService>();

        return builder;
    }
}
