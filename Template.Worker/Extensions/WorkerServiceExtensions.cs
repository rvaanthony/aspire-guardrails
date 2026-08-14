using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Template.Worker.Extensions;

public static class WorkerServiceExtensions
{
    /// <summary>
    /// Registers the contact digest worker and its options.
    /// <para>
    /// An extension rather than inline statements in <c>Program.cs</c>, matching
    /// <c>AddDataInfrastructure</c> and <c>AddBusinessServices</c>. Top-level
    /// statements cannot be called from a test, so wiring that lives there is wiring
    /// nothing can assert over — including the validation rules below, whose whole
    /// job is to fail.
    /// </para>
    /// </summary>
    public static TBuilder AddContactDigest<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);

        // Fail-fast validation: a bad interval kills startup rather than producing a
        // worker that either spins hot or never ticks.
        builder.Services
            .AddOptions<ContactDigestOptions>()
            .BindConfiguration(ContactDigestOptions.SectionName)
            .Validate(options => options.Interval > TimeSpan.Zero, "ContactDigest:Interval must be greater than zero.")
            .Validate(options => options.Take > 0, "ContactDigest:Take must be greater than zero.")
            .ValidateOnStart();

        builder.Services.AddHostedService<ContactDigestWorker>();

        return builder;
    }
}
