using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GithubActionPinner.Core
{
    public class GithubRepositoryBrowser : IGithubRepositoryBrowser
    {
        private readonly HttpClient _httpClient;

        public GithubRepositoryBrowser(string? oauthToken)
        {
            _httpClient = new HttpClient
            {
                BaseAddress = new Uri("https://api.github.com/")
            };
            // https://developer.github.com/v3/#current-version
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3+json"));
            // https://developer.github.com/v3/#user-agent-required
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GithubActionPinner", "v1"));
            if (oauthToken != null)
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", oauthToken);
        }

        public async Task<bool> IsPublicAsync(string owner, string repository, CancellationToken cancellationToken)
        {
            var response = await _httpClient.GetAsync($"repos/{owner}/{repository}", cancellationToken);

            // if not found either does not exist or private; can't tell and don't care
            return response.StatusCode == System.Net.HttpStatusCode.OK;
        }

        /// <summary>
        /// For a given branch name gets the SHA of the latest commit on it.
        /// </summary>
        public Task<string> GetShaForLatestCommitAsync(string owner, string repository, string branchName, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// For a tag of a given major version this will look for the latest SemVer compliant tag and return its current SHA.
        /// It will also check the major tag itself and return the sha for whichever commit is newer.
        /// Returns null if no newer version was found.
        /// </summary>
        /// <example>
        /// Current tag is v1.1
        /// Will list all tags and find tag v1.2 -> returns its sha
        /// Will list all tags and finds tag v1 is on a newer commit -> returns its sha
        /// Will list all tags and finds tag v1 and v1.2 (v1 is on a newer commit) -> returns its sha
        /// </example>
        public async Task<(string tag, string sha)?> GetShaForLatestSemVerCompliantCommitAsync(string owner, string repository, string tag, CancellationToken cancellationToken)
        {
            if (!tag.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException($"Unsupported version tag {tag}");

            if (!VersionHelper.TryParse(tag, out var version))
                throw new NotSupportedException($"Unsupported version tag {tag} (not a parsable version)");

            var versions = await GetLargerSemVerCompliantTagsAsync(version, cancellationToken);
            if (versions.Any())
                return versions[0];

            return null;
        }

        private async Task<(string sha, DateTimeOffset createdAt)> GetCommitAsync(string owner, string repository, GitRef gitRef, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
