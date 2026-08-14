using Microsoft.Extensions.Logging.Abstractions;
using Template.Business.Services;
using Template.Business.Tests.Fakes;
using Template.Common.Models;

namespace Template.Business.Tests.Services;

public sealed class ContactIntakeServiceTests
{
    [Fact]
    public async Task SubmitAsync_PersistsViaStore()
    {
        var store = new FakeContactSubmissionStore();
        var service = new ContactIntakeService(store, NullLogger<ContactIntakeService>.Instance);

        var id = await service.SubmitAsync(new ContactSubmission
        {
            Topic = "general",
            Name = "Ada Lovelace",
            Email = "ada@example.com",
            Message = "Hello from the vertical-slice example."
        });

        Assert.False(string.IsNullOrWhiteSpace(id));
        Assert.Single(store.Added);
        Assert.Equal("ada@example.com", store.Added[0].Email);
    }
}
