using GithubActionPinner.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GithubActionPinner.Core
{
    public class WorkflowActionProcessor
    {
        private readonly ILogger _logger;
        private readonly IGithubRepositoryBrowser _githubRepositoryBrowser;

        public WorkflowActionProcessor(
            IGithubRepositoryBrowser githubRepositoryBrowser,
            ILogger<WorkflowActionProcessor> logger)
        {
            _githubRepositoryBrowser = githubRepositoryBrowser;
            _logger = logger;
        }

        public async Task ProcessAsync(string file, bool update, CancellationToken cancellationToken)
        {
            // could parse file for validity but string manipulation is much easier #famousLastWords
            var lines = await File.ReadAllLinesAsync(file, cancellationToken);
            var parser = new ActionParser();
            for (int i = 0; i < lines.Length; i++)
            {
                if (!HasActionReference(lines[i]))
                    continue;

                ActionReference actionReference;
                try
                {
                    actionReference = parser.ParseAction(lines[i]);
                }
                catch (NotSupportedException ex)
                {
                    _logger.LogWarning($"Skipping invalid line #{i}: {ex.Message}");
                    continue;
                }
                if (actionReference.ActionName.StartsWith("docker://", StringComparison.OrdinalIgnoreCase))
                {
                    // don't support docker for now
                    continue;
                }

                Func<CancellationToken, Task<(string, string)?>> resolver;
                if (actionReference.Pinned == null)
                {
                    if (actionReference.ReferenceType != ActionReferenceType.Sha)
                    {
                        // never pinned before and not a SHA -> pin @current
                        if (actionReference.ReferenceType == ActionReferenceType.Tag)
                        {
                            var tag = actionReference.ReferenceVersion;
                            resolver = async (token) => await _githubRepositoryBrowser.GetShaForLatestSemVerCompliantCommitAsync(actionReference.Owner, actionReference.Repository, tag, token);
                        }
                        else if (actionReference.ReferenceType == ActionReferenceType.Branch)
                        {
                            var branchName = actionReference.ReferenceVersion;
                            resolver = async (token) => (await _githubRepositoryBrowser.GetShaForLatestCommitAsync(actionReference.Owner, actionReference.Repository, branchName, token), branchName);
                        }
                        else
                        {
                            throw new NotSupportedException($"Unsupported pinned version {actionReference.ReferenceVersion}");
                        }
                    }
                    else
                    {
                        // SHA pinned but no reference
                        _logger.LogInformation($"Action '{actionReference.ActionName}@{actionReference.ReferenceVersion}' is not pinned. Cannot determine version, switch to a version or add a comment '# @<version>'");
                        continue;
                    }
                }
                else
                {
                    // must be SHA
                    var currentSha = actionReference.ReferenceVersion;
                    if (actionReference.Pinned.ReferenceType == ActionReferenceType.Tag)
                    {
                        // update can be:
                        //  - same version but different SHA
                        //  - new minor version
                        var currentVersion = actionReference.Pinned.ReferenceVersion;

                        resolver = async (token) => await _githubRepositoryBrowser.GetShaForLatestSemVerCompliantCommitAsync(actionReference.Owner, actionReference.Repository, currentVersion, token);
                    }
                    else if (actionReference.Pinned.ReferenceType == ActionReferenceType.Branch)
                    {
                        var branchName = actionReference.Pinned.ReferenceVersion;
                        // find latest on branch and pin it
                        resolver = async (token) => (await _githubRepositoryBrowser.GetShaForLatestCommitAsync(actionReference.Owner, actionReference.Repository, branchName, token), branchName);
                    }
                    else
                    {
                        throw new NotSupportedException($"Unsupported pinned version {actionReference.Pinned.ReferenceVersion}");
                    }
                }

                if (!await _githubRepositoryBrowser.IsPublicAsync(actionReference.Owner, actionReference.Repository, cancellationToken))
                {
                    // cannot pin private repos, so skip
                    _logger.LogWarning($"Could not find action {actionReference.ActionName}, repo is private or removed. Skipping..");
                    continue;
                }
                var response = await resolver(cancellationToken);
                if (response == null)
                {
                    _logger.LogTrace($"Action '{actionReference.ActionName}@{actionReference.ReferenceVersion}' is already up to date");
                }
                else
                {
                    var (tag, detail) = response.Value;
                    if (update)
                    {
                        _logger.LogInformation($"Updated action '{actionReference.ActionName}@{actionReference.Pinned?.ReferenceVersion ?? actionReference.ReferenceVersion}' to {tag} ({detail})");
                    }
                    else
                        _logger.LogInformation($"Action '{actionReference.ActionName}@{actionReference.Pinned?.ReferenceVersion ?? actionReference.ReferenceVersion}' can be updated to {tag} ({detail})");
                }
            }
        }

        private static bool HasActionReference(string line)
            => line.Trim().Contains("- uses:");
    }
}
