using System;
using System.Collections.Generic;
using System.IO;

namespace GithubActionPinner.Core.Config
{
    public class ActionConfig : IActionConfig
    {
        private readonly HashSet<string> _trustedCommits = new HashSet<string>();
        private readonly HashSet<string> _trustedOwners = new HashSet<string>();
        private readonly HashSet<string> _trustedRepos = new HashSet<string>();

        public void Load(string configFile)
        {
            foreach (var line in File.ReadAllLines(configFile))
            {
                if (string.IsNullOrEmpty(line) ||
                 line.StartsWith('#'))
                {
                    continue;
                }

                var ownerRepoOrCommit = line.Trim().ToLowerInvariant();

                (ownerRepoOrCommit.Split('/').Length switch
                {
                    1 => _trustedOwners,
                    2 => _trustedRepos,
                    3 => _trustedCommits,
                    _ => throw new NotSupportedException($"Unsupported content '{line}' found in {configFile}")
                }).Add(ownerRepoOrCommit);
            }
        }

        public bool IsCommitAudited(string owner, string repo, string sha)
            => _trustedCommits.Contains($"{owner}/{repo}/{sha}".ToLowerInvariant());

        public bool IsRepositoryTrusted(string owner, string repo)
            => _trustedOwners.Contains(owner.ToLowerInvariant()) || _trustedRepos.Contains($"{owner}/{repo}".ToLowerInvariant());
    }
}
