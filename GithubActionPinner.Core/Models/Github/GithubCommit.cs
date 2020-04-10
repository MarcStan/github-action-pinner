using System.Text.Json.Serialization;

namespace GithubActionPinner.Core.Models.Github
{
    /// <summary>
    /// Model for https://api.github.com/repos/:owner/:repo/git/commits/:sha
    /// </summary>
    public class GithubCommit
    {
        [JsonPropertyName("author")]
        public GithubAuthor Author { get; set; } = new GithubAuthor();
    }
}
