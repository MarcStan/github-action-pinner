using GithubActionPinner.Core.Models;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GithubActionPinner.Core
{
    /// <summary>
    /// Helper that parses yml file action references into an object model.
    /// </summary>
    public class ActionParser : IActionParser
    {
        private readonly IGithubRepositoryBrowser _githubRepositoryBrowser;

        public ActionParser(IGithubRepositoryBrowser githubRepositoryBrowser)
            => _githubRepositoryBrowser = githubRepositoryBrowser;

        public async Task<ActionReference> ParseActionAsync(string text, CancellationToken cancellationToken)
        {
            // expected format: "  - uses: <owner>/<repo>[@<version>] [# comment|# pin@<version> comment]"
            // example format:  "  - uses: actions/foo@v1 [# comment]"
            // example format:  "  - uses: actions/foo@SHA [# pin@v1 comment]"
            var actionRef = text.TrimStart();
            if (!actionRef.StartsWith("- uses:", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException($"Action references must start with '- uses:', {text} is invalid");

            actionRef = actionRef.Substring("- uses:".Length).TrimStart();
            var comment = ParseAndExtractComment(ref actionRef);

            var (version, type) = ParseVersion(ref actionRef);
            // can either be "owner/repo" or "owner/repo/subdir../foo" -> owner is first, repo second
            if (!actionRef.Contains('/'))
                throw new NotSupportedException("Action references must be a valid repository");
            if (actionRef.StartsWith("docker://", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException("Docker references are currently not supported");
            if (actionRef.StartsWith("./", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException("Local references are currently not supported");

            var parts = actionRef.Split('/');
            string owner = parts[0];
            string repo = parts[1];

            if (version == null)
            {
                // no version = default branch
                version = await _githubRepositoryBrowser.GetRepositoryDefaultBranchAsync(owner, repo, cancellationToken);
                type = ActionReferenceType.Branch;
            }

            ActionVersion? pinned = null;
            const string pinPrefix = "pin@";
            var trimmedComment = comment.TrimStart();
            if (trimmedComment.StartsWith(pinPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var idx = trimmedComment.IndexOf(' ');
                if (idx < 0)
                    idx = trimmedComment.Length;

                // pin@master or pin@tag
                var pinnedVersion = trimmedComment.Substring(pinPrefix.Length, idx - pinPrefix.Length);
                pinned = new ActionVersion
                {
                    ReferenceType = ParseType(pinnedVersion),
                    ReferenceVersion = pinnedVersion
                };
                comment = trimmedComment.Substring(idx);

                if (comment.StartsWith(' ') || comment.StartsWith('\t'))
                {
                    // take away only one whitespace from remainder because that's what we add
                    // the rest is comment from the user (best to leave alone to not break any desired formatting)
                    comment = comment.Substring(1);
                }
            }
            return new ActionReference
            {
                ActionName = actionRef,
                Comment = comment,
                ReferenceType = type,
                ReferenceVersion = version,
                Owner = owner,
                Repository = repo,
                Pinned = pinned
            };
        }

        private (string? version, ActionReferenceType type) ParseVersion(ref string actionRef)
        {
            var idx = actionRef.IndexOf('@');
            ActionReferenceType type;
            string version;
            if (idx > -1)
            {
                version = actionRef.Substring(idx + 1).TrimEnd();
                type = ParseType(version);
                actionRef = actionRef.Substring(0, idx);
                return (version, type);
            }
            return (null, ActionReferenceType.Unknown);
        }

        /// <summary>
        /// Given an action reference ("actions/foo@v1 #bar") will extract the comment.
        /// </summary>
        /// <returns>The comment if any</returns>
        private string ParseAndExtractComment(ref string actionRef)
        {
            var idx = actionRef.IndexOf('#');
            if (idx > -1)
            {
                var comment = actionRef.Substring(idx + 1);
                actionRef = actionRef.Substring(0, idx).Trim();
                return comment;
            }
            return string.Empty;
        }

        /// <summary>
        /// Give a action version will determine its type and returns it.
        /// </summary>
        private ActionReferenceType ParseType(string version)
        {
            // TODO: someone could have a branch with same naming as version or branch name same as SHA; cannot detect here without checking git history, not sure if github actions would support this case
            if (VersionHelper.TryParse(version, out _))
                return ActionReferenceType.Tag;

            // SHA-1: 40, SHA256: 64 characters, all hex
            if ((version.Length == 40 || version.Length == 64) &&
                IsHex(version))
            {
                return ActionReferenceType.Sha;
            }

            return ActionReferenceType.Branch;
        }

        private bool IsHex(string version)
            => version.All(c => char.IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));
    }
}
