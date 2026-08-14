using Microsoft.Extensions.Hosting;
using Template.Business.Extensions;

namespace Template.Business.Tests.Extensions;

public sealed class ServiceExtensionsTests
{
    [Fact]
    public void AddBusinessServices_BuildsHostWithoutError()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddBusinessServices();

        using var host = builder.Build();
        Assert.NotNull(host.Services);
    }
}
