using System.Text.Json.Serialization;

namespace GithubActionPinner.Core.Models.Github
{
    public class GitObject
    {
        [JsonPropertyName("sha")]
        public string Sha { get; set; } = "";

        [JsonPropertyName("type")]
        public string Type { get; set; } = "";

        [JsonPropertyName("url")]
        public string Url { get; set; } = "";
    }
}
