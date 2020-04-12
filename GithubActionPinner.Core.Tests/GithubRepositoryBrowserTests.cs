using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace GithubActionPinner.Core.Tests
{
    [TestClass]
    public class GithubRepositoryBrowserTests
    {
        /// <summary>
        /// https://help.github.com/en/actions/configuring-and-managing-workflows/authenticating-with-the-github_token
        /// </summary>
        private static readonly string _token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        private static readonly HttpClient _httpClient;

        static GithubRepositoryBrowserTests()
        {
            _httpClient = new HttpClient();
            // https://developer.github.com/v3/#current-version
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
            // https://developer.github.com/v3/#user-agent-required
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MarcStan%2Fgithub-action-pinner%2Fintegration-tests", "v1"));
        }

        [DataTestMethod]
        [DataRow("MarcStan", "github-action-pinner", true)]
        [DataRow("MarcStan", "definitely-does-not-exist", false)]
        [DataRow("actions", "definitely-does-not-exist", false)]
        [DataRow("actions", "checkout", true)]
        public async Task RepositoryAccessShouldBeLimitedToPublicAndOwnedRepositories(string owner, string repo, bool expected)
            => await RateLimitHandler(async () =>
            {
                IGithubRepositoryBrowser browser = new GithubRepositoryBrowser(_token);
                var isAccessible = await browser.IsRepositoryAccessibleAsync(owner, repo, CancellationToken.None);
                Assert.AreEqual(expected, isAccessible);
            });

        private async Task RateLimitHandler(Func<Task> task)
        {
            try
            {
                await task();

                // does not count against rate limit
                var response = await _httpClient.GetAsync("https://api.github.com/rate_limit");
                response.EnsureSuccessStatusCode();
                var obj = await JsonSerializer.DeserializeAsync<RatelimitResponse>(await response.Content.ReadAsStreamAsync());
                var c = obj.Resources.Core;
                // as per https://github.com/actions/toolkit/blob/1725272151f6cc845f4ae86a925c31860c2b7beb/packages/core/src/command.ts#L16
                Console.WriteLine($"##[warning]{c.Remaining}/{c.Limit} api calls remaining (resets {c.Reset}). If you have been running the tests a lot, expect them to start failing soon.");
            }
            catch (GithubApiRatelimitExceededException ex)
            {
                Assert.Fail(ex.Message +
                    "If you are running locally, consider setting a personal access token with `repo` permissions as `GITHUB_TOKEN` environment variable");
            }
        }

        private class RatelimitResponse
        {
            [JsonPropertyName("resources")]
            public ResourcesObject Resources { get; set; } = new ResourcesObject();

            public class ResourcesObject
            {
                [JsonPropertyName("core")]
                public RatelimitObject Core { get; set; } = new RatelimitObject();
            }

            public class RatelimitObject
            {
                [JsonPropertyName("limit")]
                public int Limit { get; set; }

                [JsonPropertyName("remaining")]
                public int Remaining { get; set; }

                [JsonPropertyName("reset")]
                public long Reset { get; set; }
            }
        }
    }
}
