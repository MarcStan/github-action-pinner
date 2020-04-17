using System.Text.Json.Serialization;

namespace GithubActionPinner.Core.Models.Github
{
    /// <summary>
    /// Model for https://api.github.com/repos/:owner/:repo/branches/:branch
    /// </summary>
    public class BranchInfo
    {
        [JsonPropertyName("commit")]
        public GitObject Commit { get; set; } = new GitObject();
    }
}
