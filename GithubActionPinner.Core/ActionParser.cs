using GithubActionPinner.Core.Models;
using System;
using System.Linq;

namespace GithubActionPinner.Core
{
    public class ActionParser
    {
        public ActionReference ParseAction(string text)
        {
            // expected format: "  - uses: action/foo@<version> [# comment]"
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
            if (idx < 0)
                throw new NotSupportedException($"Action reference must have a @version reference but found none in {text}");

            var version = actionRef.Substring(idx + 1).TrimEnd();
            ActionReferenceType type;
            // TODO: someone could have a branch with same naming as version or branch name same as SHA; cannot detect here without checking git history
            if (version.StartsWith("v", StringComparison.OrdinalIgnoreCase) &&
                // C# Version must be at least major and minor (1.0), but github action also supports "v1"
                (int.TryParse(version.Substring(1), out _) || Version.TryParse(version.Substring(1), out _)))
            {
                type = ActionReferenceType.Tag;
            }
            // SHA-1: 40, SHA256: 64 characters, all hex
            else if ((version.Length == 40 || version.Length == 64) &&
                IsHex(version))
            {
                type = ActionReferenceType.Sha;
            }
            else
            {
                type = ActionReferenceType.Branch;
            }
            actionRef = actionRef.Substring(0, idx);
            return new ActionReference
            {
                ActionName = actionRef,
                Comment = comment,
                ReferenceType = type,
                ReferenceVersion = version
            };
        }

        private bool IsHex(string version)
            => version.All(c => char.IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F'));
    }
}
