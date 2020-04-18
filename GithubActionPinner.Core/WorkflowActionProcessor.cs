using GithubActionPinner.Core.Config;
using GithubActionPinner.Core.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
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
        private readonly IActionConfig _trustedActions;
        private readonly Dictionary<string, string> _auditSummary = new Dictionary<string, string>();

        public WorkflowActionProcessor(
            IGithubRepositoryBrowser githubRepositoryBrowser,
            IActionConfig trustedActions,
            IActionParser actionParser,
            ILogger<WorkflowActionProcessor> logger)
        {
            _githubRepositoryBrowser = githubRepositoryBrowser;
            _trustedActions = trustedActions;
            _actionParser = actionParser;
            _logger = logger;
        }

        public async Task ProcessAsync(string file, bool update, CancellationToken cancellationToken)
        {
            _logger.LogInformation("");
            _logger.LogInformation($"{(update ? "Updating" : "Checking")} actions in '{file}':");
            int updates = 0;
            // could parse file for validity but string manipulation is much easier #famousLastWords
            var lines = await File.ReadAllLinesAsync(file, cancellationToken).ConfigureAwait(false);
            for (int i = 0; i < lines.Length; i++)
            {
                if (!HasActionReference(lines[i]))
                    continue;

                ActionReference actionReference;
                try
                {
                    actionReference = await _actionParser.ParseActionAsync(lines[i], cancellationToken).ConfigureAwait(false);
                }
                catch (NotSupportedException ex)
                {
                    _logger.LogWarning($"Skipping invalid line #{i}: {ex.Message}");
                    continue;
                }

                Func<CancellationToken, Task<(string latest, string namedReference, string sha)?>> referenceResolver;

                var currentVersion = actionReference.Pinned?.ReferenceVersion ?? actionReference.ReferenceVersion;
                // each type can either be already pinned or not
                var type = actionReference.Pinned?.ReferenceType ?? actionReference.ReferenceType;
                if (_trustedActions.IsRepositoryTrusted(actionReference.Owner, actionReference.Repository))
                {
                    // no need to pin trusted actions
                    // but check incase a new major version is available
                    var tagResponse = await _githubRepositoryBrowser.GetAvailableUpdatesAsync(actionReference.Owner, actionReference.Repository, currentVersion, cancellationToken).ConfigureAwait(false);
                    if (tagResponse == null)
                    {
                        _logger.LogError($"Action '{actionReference.ActionName}' has no tags. Cannot update!");
                    }
                    else if (tagResponse.Value.latestTag != tagResponse.Value.latestSemVerCompliantTag &&
                        tagResponse.Value.latestSemVerCompliantSha != currentVersion)
                    {
                        _logger.LogWarning($"Action '{actionReference.ActionName}@{currentVersion}' can be updated to {tagResponse.Value.latestTag}.");
                    }
                    continue;
                }
                else
                {
                    switch (type)
                    {
                        case ActionReferenceType.Branch:
                            var branchName = currentVersion;
                            referenceResolver = async (token) =>
                            {
                                var sha = await _githubRepositoryBrowser.GetShaForLatestCommitAsync(actionReference.Owner, actionReference.Repository, branchName, token).ConfigureAwait(false);
                                if (sha == null)
                                    return null; // branch no longer exists?
                                return (branchName, branchName, sha);
                            };
                            break;
                        case ActionReferenceType.Tag:
                            referenceResolver = async (token) => await _githubRepositoryBrowser.GetAvailableUpdatesAsync(actionReference.Owner, actionReference.Repository, currentVersion, token).ConfigureAwait(false);
                            break;
                        case ActionReferenceType.Sha:
                            // makes no sense to be pinned
                            _logger.LogInformation($"Action '{actionReference.ActionName}@{actionReference.ReferenceVersion}' is not pinned. Cannot determine version, switch to a version or add a comment '# @<version>'");
                            continue;
                        default:
                            throw new ArgumentOutOfRangeException(type.ToString());
                    }
                }

                if (!await _githubRepositoryBrowser.IsRepositoryAccessibleAsync(actionReference.Owner, actionReference.Repository, cancellationToken).ConfigureAwait(false))
                {
                    // cannot pin repos without access, so skip
                    _logger.LogWarning($"Could not find action {actionReference.ActionName}, repo is private or removed. Skipping..");
                    continue;
                }
                var response = await referenceResolver(cancellationToken).ConfigureAwait(false);
                if (!response.HasValue)
                {
                    _logger.LogError($"Action '{actionReference.ActionName}@{actionReference.ReferenceVersion}' has no version that exists anymore. Cannot update!");
                }
                else
                {
                    var (latestVersion, tagOrBranch, sha) = response.Value;
                    var existingRef = actionReference.Pinned?.ReferenceVersion ?? actionReference.ReferenceVersion;
                    var updateDescription = $"updated {existingRef} -> {tagOrBranch}";
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
                        updateDescription = $"pinned to {existingRef} ({updateType} SHA {sha})";
                    }
                    updates++;
                    if (update)
                    {
                        lines[i] = UpdateLine(lines[i], actionReference, sha, tagOrBranch);
                        _logger.LogInformation($"(Line {i + 1}): Action '{actionReference.ActionName}' was {updateDescription}.");
                    }
                    else
                    {
                        _logger.LogInformation($"(Line {i + 1}): Action '{actionReference.ActionName}' can be {updateDescription}.");
                    }
                    if (latestVersion != null &&
                        type == ActionReferenceType.Tag &&
                        latestVersion != tagOrBranch &&
                        VersionHelper.TryParse(latestVersion, out var latest) &&
                        VersionHelper.TryParse(tagOrBranch, out var target) &&
                        latest.Major != target.Major)
                    {
                        // warn about major upgrades (user must perform them manually)
                        _logger.LogWarning($"Action '{actionReference.ActionName}@{currentVersion}' can be upgraded to {latestVersion} (perform upgrade manually due to possible breaking changes).");
                    }
                    if (!_trustedActions.IsCommitAudited(actionReference.Owner, actionReference.Repository, sha))
                    {
                        _auditSummary[$"{actionReference.ActionName}/{sha}".ToLowerInvariant()] =
                            $"Consider adding '{actionReference.ActionName}/{sha}' ({tagOrBranch}) to the audit log once you have audited the code!";
                    }
                }
            }
            if (updates > 0)
            {
                _logger.LogInformation($"{updates} actions {(update ? "have been updated" : "need to be updated")}.");
            }
            if (update)
            {
                await File.WriteAllLinesAsync(file, lines, cancellationToken).ConfigureAwait(false);
            }
        }

        public void Summarize()
        {
            _logger.LogInformation("");
            _logger.LogInformation("Audit summary:");
            foreach (var entry in _auditSummary)
                _logger.LogWarning(entry.Value);
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
