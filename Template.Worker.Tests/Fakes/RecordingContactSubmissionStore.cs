using Template.Common.Interfaces;
using Template.Common.Models;

namespace Template.Worker.Tests.Fakes;

/// <summary>
/// Counts <see cref="ListNewestAsync"/> calls and signals once a target count is
/// reached, so a test can wait on the worker's ticks instead of sleeping for a
/// guessed duration. Optionally throws on one nominated call, which is how the
/// worker's "a bad tick must not kill the loop" behavior gets exercised.
/// </summary>
public sealed class RecordingContactSubmissionStore : IContactSubmissionStore
{
    private readonly Lock _gate = new();
    private readonly TaskCompletionSource _targetReached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly int _targetCalls;
    private readonly int _throwOnCall;
    private readonly List<int> _takes = [];
    private int _calls;

    /// <param name="targetCalls">Completes <see cref="TargetReached"/> on this call.</param>
    /// <param name="throwOnCall">1-based call number to throw on; 0 never throws.</param>
    public RecordingContactSubmissionStore(int targetCalls, int throwOnCall = 0)
    {
        _targetCalls = targetCalls;
        _throwOnCall = throwOnCall;
    }

    /// <summary>Completes once <c>targetCalls</c> calls have been recorded.</summary>
    public Task TargetReached => _targetReached.Task;

    /// <summary>The <c>take</c> argument of every recorded call, in order.</summary>
    public IReadOnlyList<int> Takes
    {
        get
        {
            lock (_gate)
            {
                return [.. _takes];
            }
        }
    }

    public Task<string> AddAsync(ContactSubmission submission, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("The digest worker only reads.");

    public Task<IReadOnlyList<ContactSubmission>> ListNewestAsync(int take, CancellationToken cancellationToken = default)
    {
        int call;
        lock (_gate)
        {
            _takes.Add(take);
            call = ++_calls;
        }

        // Signal before throwing, so a test that plants a failure can still wait
        // on the call count that includes it.
        if (call >= _targetCalls)
        {
            _targetReached.TrySetResult();
        }

        if (call == _throwOnCall)
        {
            throw new InvalidOperationException("Simulated transient store failure.");
        }

        return Task.FromResult<IReadOnlyList<ContactSubmission>>([]);
    }
}
