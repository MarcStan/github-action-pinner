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
        private readonly IActionParser _actionParser;

        public WorkflowActionProcessor(
            IGithubRepositoryBrowser githubRepositoryBrowser,
            IActionParser actionParser,
            ILogger<WorkflowActionProcessor> logger)
        {
            _githubRepositoryBrowser = githubRepositoryBrowser;
            _actionParser = actionParser;
            _logger = logger;
        }

        public async Task ProcessAsync(string file, bool update, CancellationToken cancellationToken)
        {
            // could parse file for validity but string manipulation is much easier #famousLastWords
            var lines = await File.ReadAllLinesAsync(file, cancellationToken);
            for (int i = 0; i < lines.Length; i++)
            {
                if (!HasActionReference(lines[i]))
                    continue;

                ActionReference actionReference;
                try
                {
                    actionReference = await _actionParser.ParseActionAsync(lines[i], cancellationToken);
                }
                catch (NotSupportedException ex)
                {
                    _logger.LogWarning($"Skipping invalid line #{i}: {ex.Message}");
                    continue;
                }

                Func<CancellationToken, Task<(string namedReference, string sha)?>> referenceResolver;

                var currentVersion = actionReference.Pinned?.ReferenceVersion ?? actionReference.ReferenceVersion;
                // each type can either be already pinned or not
                var type = actionReference.Pinned?.ReferenceType ?? actionReference.ReferenceType;
                switch (type)
                {
                    case ActionReferenceType.Branch:
                        var branchName = currentVersion;
                        referenceResolver = async (token) => (branchName, await _githubRepositoryBrowser.GetShaForLatestCommitAsync(actionReference.Owner, actionReference.Repository, branchName, token));
                        break;
                    case ActionReferenceType.Tag:
                        var tag = currentVersion;
                        referenceResolver = async (token) => await _githubRepositoryBrowser.GetLatestSemVerCompliantAsync(actionReference.Owner, actionReference.Repository, tag, token);
                        break;
                    case ActionReferenceType.Sha:
                        // makes no sense to be pinned
                        _logger.LogInformation($"Action '{actionReference.ActionName}@{actionReference.ReferenceVersion}' is not pinned. Cannot determine version, switch to a version or add a comment '# @<version>'");
                        continue;
                    default:
                        throw new ArgumentOutOfRangeException(type.ToString());
                }

                if (!await _githubRepositoryBrowser.IsRepositoryAccessibleAsync(actionReference.Owner, actionReference.Repository, cancellationToken))
                {
                    // cannot pin repos without access, so skip
                    _logger.LogWarning($"Could not find action {actionReference.ActionName}, repo is private or removed. Skipping..");
                    continue;
                }
                var response = await referenceResolver(cancellationToken);
                if (response == null)
                {
                    _logger.LogTrace($"Action '{actionReference.ActionName}@{actionReference.ReferenceVersion}' is already up to date.");
                }
                else
                {
                    var (tagOrBranch, sha) = response.Value;
                    var existingRef = actionReference.Pinned?.ReferenceVersion ?? actionReference.ReferenceVersion;
                    var desired = $"updated {existingRef} -> {tagOrBranch}";
                    if (existingRef == tagOrBranch)
                    {
                        if (actionReference.Pinned != null &&
                            actionReference.ReferenceVersion == sha)
                        {
                            // no update required
                            continue;
                        }
                        // update like v1 -> v1 or master -> master look confusing to user
                        // show the underlying sha change instead

                        // modify wording depending on first pin or update of SHA
                        var updateType = actionReference.Pinned == null ? "using" : "updated to";
                        desired = $"pinned to {existingRef} ({updateType} SHA {sha})";
                    }
                    if (update)
                    {
                        lines[i] = UpdateLine(lines[i], actionReference, sha, tagOrBranch);
                        _logger.LogInformation($"(Line {i + 1}): Action '{actionReference.ActionName}' was {desired}.");
                    }
                    else
                    {
                        _logger.LogInformation($"(Line {i + 1}): Action '{actionReference.ActionName}' can be {desired}.");
                    }
                }
            }
            if (update)
                await File.WriteAllLinesAsync(file, lines, cancellationToken);
        }

        private string UpdateLine(string line, ActionReference actionReference, string sha, string pinned)
        {
            var prefix = line.Substring(0, line.IndexOf(actionReference.ActionName));

            return $"{prefix}{actionReference.ActionName}@{sha} # pin@{pinned} {actionReference.Comment}";
        }

        private static bool HasActionReference(string line)
            => line.Trim().Contains("- uses:");
    }
}
