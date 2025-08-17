using Microsoft.Extensions.Logging;

namespace RazorX.Framework.Tests.Mocks;

public sealed class TestLogger<T> : ILogger<T> {
    public List<string> LogMessages { get; } = [];
    public LogLevel LastLogLevel { get; private set; }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter) {
        LastLogLevel = logLevel;
        var message = formatter(state, exception);
        LogMessages.Add(message);
    }

    public void Clear() {
        LogMessages.Clear();
        LastLogLevel = LogLevel.None;
    }
}