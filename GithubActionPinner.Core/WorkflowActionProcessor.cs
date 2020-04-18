using GithubActionPinner.Core.Config;
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
        private readonly LogCollector _auditLogger;
        private readonly LogCollector _summaryLogger;
        private readonly IGithubRepositoryBrowser _githubRepositoryBrowser;
        private readonly IActionParser _actionParser;
        private readonly IActionConfig _trustedActions;
        private readonly ILogger<WorkflowActionProcessor> _logger;

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

            _auditLogger = new LogCollector(logger);
            _summaryLogger = new LogCollector(logger);
        }

        public async Task ProcessAsync(string file, bool update, CancellationToken cancellationToken)
        {
            LogInformation("");
            LogInformation($"{(update ? "Updating" : "Checking")} actions in '{file}':");
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
                    _summaryLogger.LogWarning($"{file}/{i}", $"Skipping invalid line #{i}: {ex.Message}");
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
                        _summaryLogger.LogError($"{actionReference.ActionName}", $"Action '{actionReference.ActionName}' has no tags. Cannot update!");
                    }
                    else if (tagResponse.Value.latestTag != tagResponse.Value.latestSemVerCompliantTag &&
                             tagResponse.Value.latestSemVerCompliantSha != currentVersion)
                    {
                        _summaryLogger.LogWarning($"{actionReference.ActionName}@{currentVersion}", $"Action '{actionReference.ActionName}@{currentVersion}' can be updated to {tagResponse.Value.latestTag}.");
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
                            LogInformation($"Action '{actionReference.ActionName}@{actionReference.ReferenceVersion}' is not pinned. Cannot determine version, switch to a version or add a comment '# @<version>'");
                            continue;
                        default:
                            throw new ArgumentOutOfRangeException(type.ToString());
                    }
                }

                if (!await _githubRepositoryBrowser.IsRepositoryAccessibleAsync(actionReference.Owner, actionReference.Repository, cancellationToken).ConfigureAwait(false))
                {
                    // cannot pin repos without access, so skip
                    _summaryLogger.LogError(actionReference.ActionName, $"Could not find action {actionReference.ActionName}, repo is private or removed. Skipping..");
                    continue;
                }
                var response = await referenceResolver(cancellationToken).ConfigureAwait(false);
                if (!response.HasValue)
                {
                    _summaryLogger.LogError(actionReference.ActionName, $"Action '{actionReference.ActionName}@{actionReference.ReferenceVersion}' has no version that exists anymore. Cannot update!");
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
                        LogInformation($"(Line {i + 1}): Action '{actionReference.ActionName}' was {updateDescription}.");
                    }
                    else
                    {
                        LogInformation($"(Line {i + 1}): Action '{actionReference.ActionName}' can be {updateDescription}.");
                    }
                    if (latestVersion != null &&
                        type == ActionReferenceType.Tag &&
                        latestVersion != tagOrBranch &&
                        VersionHelper.TryParse(latestVersion, out var latest) &&
                        VersionHelper.TryParse(tagOrBranch, out var target) &&
                        latest.Major != target.Major)
                    {
                        // warn about major upgrades (user must perform them manually)
                        _summaryLogger.LogWarning($"{actionReference.ActionName}@{currentVersion}", $"Action '{actionReference.ActionName}@{currentVersion}' can be upgraded to {latestVersion} (perform upgrade manually due to possible breaking changes).");
                    }
                    if (!_trustedActions.IsCommitAudited(actionReference.Owner, actionReference.Repository, sha))
                    {
                        _auditLogger.LogWarning($"{actionReference.ActionName}/{sha}", $"Consider adding '{actionReference.ActionName}/{sha}' ({tagOrBranch}) to the audit log once you have audited the code!");
                    }
                }
            }
            if (updates > 0)
            {
                LogInformation($"{updates} actions {(update ? "have been updated" : "need to be updated")}.");
            }
            if (update)
            {
                await File.WriteAllLinesAsync(file, lines, cancellationToken).ConfigureAwait(false);
            }
        }

        private void LogInformation(string message)
            => _logger.LogInformation(message);

        public void Summarize()
        {
            LogInformation("");
            LogInformation("Issues:");
            _summaryLogger.Summarize();

            LogInformation("");
            LogInformation("Audit summary:");
            _auditLogger.Summarize();
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
