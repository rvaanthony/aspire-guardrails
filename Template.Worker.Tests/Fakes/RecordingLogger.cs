using Microsoft.Extensions.Logging;

namespace Template.Worker.Tests.Fakes;

/// <summary>
/// Captures log entries so a test can assert that a swallowed exception was
/// actually reported. Without this, "the worker survives a bad tick" and "the
/// worker silently eats failures" are the same passing test.
/// </summary>
public sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly Lock _gate = new();
    private readonly List<(LogLevel Level, string Message, Exception? Exception)> _entries = [];

    public IReadOnlyList<(LogLevel Level, string Message, Exception? Exception)> Entries
    {
        get
        {
            lock (_gate)
            {
                return [.. _entries];
            }
        }
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        lock (_gate)
        {
            _entries.Add((logLevel, formatter(state, exception), exception));
        }
    }
}
