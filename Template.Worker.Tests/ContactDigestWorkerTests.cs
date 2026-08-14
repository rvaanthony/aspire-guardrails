using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Template.Business.Services;
using Template.Common.Interfaces;
using Template.Worker.Tests.Fakes;

namespace Template.Worker.Tests;

public sealed class ContactDigestWorkerTests
{
    // Short enough that three ticks land well inside WaitTimeout, long enough not
    // to saturate a loaded CI runner with timer callbacks.
    private static readonly TimeSpan TickInterval = TimeSpan.FromMilliseconds(15);

    // Generous on purpose: the tests wait on a signal, not on this, so a high
    // value costs nothing when things work and only bounds a hang.
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task TicksRepeatedlyAndAsksForTheConfiguredTake()
    {
        var store = new RecordingContactSubmissionStore(targetCalls: 3);
        var (worker, _) = CreateWorker(store, new ContactDigestOptions { Interval = TickInterval, Take = 7 });

        await worker.StartAsync(CancellationToken.None);
        try
        {
            await store.TargetReached.WaitAsync(WaitTimeout);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }

        var takes = store.Takes;
        Assert.True(takes.Count >= 3, $"Expected at least 3 ticks, saw {takes.Count}.");
        Assert.All(takes, take => Assert.Equal(7, take));
    }

    [Fact]
    public async Task ATickThatThrowsIsLoggedAndTheLoopKeepsRunning()
    {
        // Throw on the first tick, then require two more — if the catch block were
        // removed, ExecuteAsync would unwind on tick 1 and the target would never
        // be reached.
        var store = new RecordingContactSubmissionStore(targetCalls: 3, throwOnCall: 1);
        var (worker, logger) = CreateWorker(store, new ContactDigestOptions { Interval = TickInterval });

        await worker.StartAsync(CancellationToken.None);
        try
        {
            await store.TargetReached.WaitAsync(WaitTimeout);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }

        Assert.True(store.Takes.Count >= 3, $"Expected the loop to survive, saw {store.Takes.Count} tick(s).");

        var errors = logger.Entries.Where(entry => entry.Level == LogLevel.Error).ToArray();
        var reported = Assert.Single(errors);
        Assert.IsType<InvalidOperationException>(reported.Exception);
    }

    [Fact]
    public async Task StoppingEndsTheLoopWithoutFaulting()
    {
        var store = new RecordingContactSubmissionStore(targetCalls: 1);
        var (worker, logger) = CreateWorker(store, new ContactDigestOptions { Interval = TickInterval });

        await worker.StartAsync(CancellationToken.None);
        await store.TargetReached.WaitAsync(WaitTimeout);
        await worker.StopAsync(CancellationToken.None);

        // Cancellation is an ordinary shutdown, not a failure: the exception filter
        // in the worker excludes OperationCanceledException precisely so that
        // stopping does not log an error.
        Assert.NotNull(worker.ExecuteTask);
        Assert.True(worker.ExecuteTask.IsCompleted);
        Assert.False(worker.ExecuteTask.IsFaulted);
        Assert.DoesNotContain(logger.Entries, entry => entry.Level == LogLevel.Error);
    }

    [Fact]
    public async Task ResolvesAFreshScopePerTick()
    {
        var store = new RecordingContactSubmissionStore(targetCalls: 2);
        var scopeFactory = new CountingScopeFactory(BuildServiceProvider(store));
        var worker = new ContactDigestWorker(
            scopeFactory,
            Options.Create(new ContactDigestOptions { Interval = TickInterval }),
            NullLogger<ContactDigestWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        try
        {
            await store.TargetReached.WaitAsync(WaitTimeout);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }

        // ContactIntakeService is scoped, so a shared scope would leak a DbContext
        // (or its Tables equivalent) across every tick for the worker's lifetime.
        Assert.True(
            scopeFactory.ScopesCreated >= store.Takes.Count,
            $"Expected one scope per tick; {scopeFactory.ScopesCreated} scope(s) for {store.Takes.Count} tick(s).");
    }

    private static (ContactDigestWorker Worker, RecordingLogger<ContactDigestWorker> Logger) CreateWorker(
        RecordingContactSubmissionStore store,
        ContactDigestOptions options)
    {
        var provider = BuildServiceProvider(store);
        var logger = new RecordingLogger<ContactDigestWorker>();
        var worker = new ContactDigestWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(options),
            logger);

        return (worker, logger);
    }

    private static ServiceProvider BuildServiceProvider(RecordingContactSubmissionStore store)
    {
        var services = new ServiceCollection();
        services.AddSingleton<IContactSubmissionStore>(store);
        services.AddSingleton<ILogger<ContactIntakeService>>(NullLogger<ContactIntakeService>.Instance);
        services.AddScoped<ContactIntakeService>();
        return services.BuildServiceProvider();
    }

    private sealed class CountingScopeFactory : IServiceScopeFactory
    {
        private readonly IServiceScopeFactory _inner;
        private int _scopesCreated;

        public CountingScopeFactory(IServiceProvider provider) =>
            _inner = provider.GetRequiredService<IServiceScopeFactory>();

        public int ScopesCreated => Volatile.Read(ref _scopesCreated);

        public IServiceScope CreateScope()
        {
            Interlocked.Increment(ref _scopesCreated);
            return _inner.CreateScope();
        }
    }
}
