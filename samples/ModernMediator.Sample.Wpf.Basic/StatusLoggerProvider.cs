using Microsoft.Extensions.Logging;

namespace ModernMediator.Sample.Wpf.Basic;

public class StatusLoggerProvider : ILoggerProvider
{
    private readonly StatusLogService _service;

    public StatusLoggerProvider(StatusLogService service) => _service = service;

    public ILogger CreateLogger(string categoryName) => new StatusLogger(_service, categoryName);

    public void Dispose() { }
}

public class StatusLogger : ILogger
{
    private readonly StatusLogService _service;
    private readonly string _categoryName;

    public StatusLogger(StatusLogService service, string categoryName)
    {
        _service = service;
        _categoryName = categoryName;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => _categoryName.Contains("LoggingBehavior");

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel)) return;
        _service.AddEntry(formatter(state, exception));
    }
}
