using System.Text.Json.Serialization;

namespace GithubActionPinner.Core.Models.Github
{
    /// <summary>
    /// Model for https://api.github.com/repos/:owner/:repo
    /// </summary>
    public class GithubRepository
    {
        [JsonPropertyName("default_branch")]
        public string DefaultBranch { get; set; } = "";
    }
}
