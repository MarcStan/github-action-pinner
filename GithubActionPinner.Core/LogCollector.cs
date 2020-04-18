using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace GithubActionPinner.Core
{
    public class LogCollector
    {
        private readonly ILogger _logger;
        private readonly Dictionary<string, string> _messages = new Dictionary<string, string>();

        public LogCollector(ILogger logger)
            => _logger = logger;

        public void LogWarning(string category, string message)
            => _logger.LogWarning(_messages[category.ToLowerInvariant()] = message);

        public void LogError(string category, string message)
            => _logger.LogError(_messages[category.ToLowerInvariant()] = message);

        public void Summarize()
        {
            foreach (var m in _messages)
                _logger.LogWarning(m.Value);
        }
    }
}
