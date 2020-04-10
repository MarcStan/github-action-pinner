using System.Text.Json.Serialization;

namespace GithubActionPinner.Core.Models.Github
{
    /// <summary>
    /// Model for https://api.github.com/repos/:owner/:repo/git/refs/tags
    /// </summary>
    public class GithubTag
    {
        [JsonPropertyName("object")]
        public GitObject Object { get; set; } = new GitObject();
    }
}
