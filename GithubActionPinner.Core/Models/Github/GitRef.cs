using System.Text.Json.Serialization;

namespace GithubActionPinner.Core.Models.Github
{
    public class GitRef
    {
        [JsonPropertyName("ref")]
        public string Ref { get; set; } = "";

        [JsonPropertyName("object")]
        public GitObject Object { get; set; } = new GitObject();
    }
}
