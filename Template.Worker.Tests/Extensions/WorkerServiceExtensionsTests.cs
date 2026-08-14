using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Template.Worker.Extensions;

namespace Template.Worker.Tests.Extensions;

public sealed class WorkerServiceExtensionsTests
{
    [Fact]
    public void AddContactDigest_RegistersTheWorkerAsAHostedService()
    {
        using var host = BuildHost();

        var hostedServices = host.Services.GetServices<IHostedService>();

        Assert.Contains(hostedServices, service => service is ContactDigestWorker);
    }

    [Fact]
    public void AddContactDigest_BindsIntervalAndTakeFromConfiguration()
    {
        using var host = BuildHost(new Dictionary<string, string?>
        {
            ["ContactDigest:Interval"] = "00:02:30",
            ["ContactDigest:Take"] = "42"
        });

        var options = host.Services.GetRequiredService<IOptions<ContactDigestOptions>>().Value;

        Assert.Equal(TimeSpan.FromSeconds(150), options.Interval);
        Assert.Equal(42, options.Take);
    }

    [Fact]
    public void AddContactDigest_FallsBackToItsDefaultsWhenNothingIsConfigured()
    {
        using var host = BuildHost();

        var options = host.Services.GetRequiredService<IOptions<ContactDigestOptions>>().Value;

        // A worker with no configuration section must still be sane rather than
        // ticking every zero seconds.
        Assert.Equal(TimeSpan.FromMinutes(15), options.Interval);
        Assert.Equal(5, options.Take);
    }

    [Theory]
    [InlineData("00:00:00", "5", "Interval")]
    [InlineData("-00:00:01", "5", "Interval")]
    [InlineData("00:15:00", "0", "Take")]
    [InlineData("00:15:00", "-3", "Take")]
    public async Task AddContactDigest_RefusesToStartOnAnInvalidConfiguration(
        string interval, string take, string expectedInMessage)
    {
        using var host = BuildHost(new Dictionary<string, string?>
        {
            ["ContactDigest:Interval"] = interval,
            ["ContactDigest:Take"] = take
        });

        // StartAsync, not resolving the options, because ValidateOnStart is the
        // promise being tested: a misconfigured deploy should die on startup rather
        // than at the first tick fifteen minutes later, where it would surface as a
        // worker that silently never ran.
        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());

        Assert.Contains(expectedInMessage, string.Join(" ", exception.Failures), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AddContactDigest_StartsOnAValidConfiguration()
    {
        // The control for the theory above. Without it, an AddContactDigest that
        // refused every configuration would look correct.
        using var host = BuildHost(new Dictionary<string, string?>
        {
            ["ContactDigest:Interval"] = "00:00:30",
            ["ContactDigest:Take"] = "5"
        });

        await host.StartAsync();
        await host.StopAsync();
    }

    [Fact]
    public void AddContactDigest_RejectsANullBuilder()
    {
        Assert.Throws<ArgumentNullException>(() =>
            WorkerServiceExtensions.AddContactDigest<IHostApplicationBuilder>(null!));
    }

    private static IHost BuildHost(Dictionary<string, string?>? configuration = null)
    {
        var builder = Host.CreateApplicationBuilder();
        if (configuration is not null)
        {
            builder.Configuration.AddInMemoryCollection(configuration);
        }

        // Only AddContactDigest is exercised here. ContactIntakeService is
        // deliberately absent: the worker resolves it per tick inside a try/catch, so
        // its absence cannot break startup — which is what these tests are about.
        builder.AddContactDigest();

        return builder.Build();
    }
}
