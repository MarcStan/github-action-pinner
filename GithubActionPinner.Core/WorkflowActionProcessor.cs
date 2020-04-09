﻿using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GithubActionPinner.Core
{
    public class WorkflowActionProcessor
    {
        private readonly ILogger _logger;

        public WorkflowActionProcessor(ILogger logger)
            => _logger = logger;

        public async Task ProcessAsync(string file, bool update, CancellationToken cancellationToken)
        {
            // could parse file for validity but string manipulation is much easier #famousLastWords
            var lines = await File.ReadAllLinesAsync(file, cancellationToken);
            var parser = new ActionParser();
            for (int i = 0; i < lines.Length; i++)
            {
                if (!HasActionReference(lines[i]))
                    continue;

                try
                {
                    var info = parser.ParseAction(lines[i]);
                }
                catch (NotSupportedException ex)
                {
                    _logger.LogWarning($"Skipping invalid line #{i}: {ex.Message}");
                    continue;
                }
                //if (info.IsDocker || !info.IsPublic)
                //    continue;

                //if (info.Pinned.HasValue)
                //{
                //    var currentSha = info.Sha;
                //    if (info.Pinned.Value.VersionReferenceType == ActionVersionReferenceType.Tag)
                //    {
                //        // update can be:
                //        //  - same version but different SHA
                //        //  - new minor version
                //        var currentVersion = info.Pinned.Value.Version;
                //    }
                //}
                //else if (info.VersionReferenceType != ActionVersionReferenceType.SHA)
                //{
                //    // never pinned before and not a SHA -> pin @current
                //}
            }
        }

        private static bool HasActionReference(string line)
            => line.Trim().Contains("- uses:");
    }
}
