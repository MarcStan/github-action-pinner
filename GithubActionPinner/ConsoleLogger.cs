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
        {
            var old = Console.ForegroundColor;
            var desired = logLevel switch
            {
                LogLevel.Warning => ConsoleColor.Yellow,
                LogLevel.Error => ConsoleColor.Red,
                LogLevel.Critical => ConsoleColor.DarkRed,
                _ => old
            };
            if (old != desired)
                Console.ForegroundColor = desired;
            Console.WriteLine(formatter(state, exception));

            Console.ForegroundColor = old;
        }
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
