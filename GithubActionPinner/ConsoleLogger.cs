using Microsoft.Extensions.Logging;
using System;

namespace GithubActionPinner
{
    public class ConsoleLogger : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            => null;

        public bool IsEnabled(LogLevel logLevel)
            => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
            => Console.WriteLine(formatter(state, exception));
    }

    public class ConsoleLoggerProvider : ILoggerProvider
    {
        private readonly ConsoleLogger _logger = new ConsoleLogger();

        public ILogger CreateLogger(string categoryName)
            => _logger;

        public void Dispose()
        {
        }
    }
}
