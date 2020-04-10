using System;
using System.Text.Json.Serialization;

namespace GithubActionPinner.Core.Models.Github
{
    public class GithubAuthor
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("date")]
        public DateTimeOffset Date { get; set; }
    }
}
