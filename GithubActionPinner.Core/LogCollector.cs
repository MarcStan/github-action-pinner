using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace GithubActionPinner.Core
{
    public class LogCollector
    {
        private readonly ILogger _logger;
        private readonly Dictionary<string, (LogLevel level, string message)> _messages = new Dictionary<string, (LogLevel level, string message)>();

        public LogCollector(ILogger logger)
            => _logger = logger;

        public void LogWarning(string category, string message)
        {
            _messages[category.ToLowerInvariant()] = (LogLevel.Warning, message);
            _logger.LogWarning(message);
        }

        public void LogError(string category, string message)
        {
            _messages[category.ToLowerInvariant()] = (LogLevel.Error, message);
            _logger.LogError(message);
        }

        public void Summarize()
        {
            foreach (var m in _messages)
            {
                switch (m.Value.level)
                {
                    case LogLevel.Warning:
                        _logger.LogWarning(m.Value.message);
                        break;
                    case LogLevel.Error:
                        _logger.LogError(m.Value.message);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(m.Value.level.ToString());
                }
            }
        }
    }
}
