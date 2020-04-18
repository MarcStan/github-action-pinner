using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
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

        [DataTestMethod]
        [DataRow("MarcStan", "github-action-pinner", true)]
        [DataRow("MarcStan", "definitely-does-not-exist", false)]
        [DataRow("actions", "definitely-does-not-exist", false)]
        [DataRow("actions", "checkout", true)]
        public async Task RepositoryAccessShouldBeLimitedToPublicAndOwnedRepositories(string owner, string repo, bool expected)
            => await RateLimitHandler(async () =>
            {
                IGithubRepositoryBrowser browser = new GithubRepositoryBrowser(new CachedGithubApi(_token));
                var isAccessible = await browser.IsRepositoryAccessibleAsync(owner, repo, CancellationToken.None);
                Assert.AreEqual(expected, isAccessible);
            });

        private async Task RateLimitHandler(Func<Task> task)
        {
            try
            {
                await task();
            }
            catch (GithubApiRatelimitExceededException ex)
            {
                Assert.Fail(ex.Message + Environment.NewLine +
                    "If you are running locally, consider setting a personal access token with `repo` permissions as `GITHUB_TOKEN` environment variable.");
            }
        }
    }
}
