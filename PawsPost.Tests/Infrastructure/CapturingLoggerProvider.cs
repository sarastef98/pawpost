using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace PawsPost.Tests.Infrastructure;

/// <summary>Collects every log message (with its exception text) so tests can check what was logged.</summary>
public sealed class CapturingLoggerProvider : ILoggerProvider
{
    public ConcurrentQueue<string> Messages { get; } = new();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(Messages);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(ConcurrentQueue<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            messages.Enqueue($"{logLevel}|{formatter(state, exception)}{exception}");
        }
    }
}
