using GithubActionPinner.Core.Models;
using System;
using System.Linq;

namespace GithubActionPinner.Core
{
    /// <summary>
    /// Helper that parses yml file action references into an object model.
    /// </summary>
    public class ActionParser
    {
        public ActionReference ParseAction(string text)
        {
            // expected format: "  - uses: <owner>/<repo>[@<version>] [# comment]"
            // example format:  "  - uses: actions/foo@v1 [# comment]"
            var actionRef = text.Trim();
            if (!actionRef.StartsWith("- uses:"))
                throw new NotSupportedException($"Action references must start with '- uses:', {text} is invalid");

            actionRef = actionRef.Substring("- uses:".Length).TrimStart();
            string comment = "";
            var idx = actionRef.IndexOf('#');
            if (idx > -1)
            {
                comment = actionRef.Substring(idx + 1).TrimStart();
                actionRef = actionRef.Substring(0, idx).TrimEnd();
            }
            idx = actionRef.IndexOf('@');
            ActionReferenceType type;
            string version;
            if (idx > -1)
            {
                version = actionRef.Substring(idx + 1).TrimEnd();
                type = ParseType(version);
                actionRef = actionRef.Substring(0, idx);
            }
            else
            {
                // no reference = default branch
                type = ActionReferenceType.Branch;
                // TODO: default branch can be changed on github
                version = "master";
            }
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
            ActionVersion? pinned = null;
            if (comment.StartsWith('@'))
            {
                idx = comment.IndexOf(' ');
                if (idx < 0)
                    idx = comment.Length;

                // @master or @tag
                var pinnedVersion = comment.Substring(1, idx - 1);
                pinned = new ActionVersion
                {
                    ReferenceType = ParseType(pinnedVersion),
                    ReferenceVersion = pinnedVersion
                };
                comment = comment.Substring(idx);

                // take away only one space from remainder because that's what we add, the rest is comment from the user; best to leave alone
                if (comment.StartsWith(' '))
                    comment = comment.Substring(1);
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

        private ActionReferenceType ParseType(string version)
        {
            // TODO: someone could have a branch with same naming as version or branch name same as SHA; cannot detect here without checking git history
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
