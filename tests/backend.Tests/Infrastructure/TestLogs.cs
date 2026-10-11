using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace backend.Tests.Infrastructure;

/// <summary>
/// Keeps every log message the app writes, so tests can check what reaches the log and what must not.
/// </summary>
public sealed class TestLogs : ILoggerProvider
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();

    public IReadOnlyList<LogEntry> Entries => _entries.ToArray();

    public ILogger CreateLogger(string categoryName) => new Logger(categoryName, _entries);

    public void Dispose()
    {
    }

    public sealed record LogEntry(string Category, LogLevel Level, EventId EventId, string Message, IReadOnlyDictionary<string, object?> State);

    private sealed class Logger(string category, ConcurrentQueue<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            var values = state is IEnumerable<KeyValuePair<string, object?>> pairs
                ? pairs.ToDictionary(p => p.Key, p => p.Value)
                : new Dictionary<string, object?>();

            entries.Enqueue(new LogEntry(category, logLevel, eventId, formatter(state, exception), values));
        }
    }
}
