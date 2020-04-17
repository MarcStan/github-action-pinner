using GithubActionPinner.Core.Models.Github;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text.Json;
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
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("MarcStan%2Fgithub-action-pinner", "v1"));
            if (oauthToken != null)
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("token", oauthToken);
        }

        public async Task<bool> IsRepositoryAccessibleAsync(string owner, string repository, CancellationToken cancellationToken)
        {
            var response = await _httpClient.GetAsync($"repos/{owner}/{repository}", cancellationToken);
            CheckRateLimit(response);
            return response.StatusCode == System.Net.HttpStatusCode.OK;
        }

        public async Task<string> GetRepositoryDefaultBranchAsync(string owner, string repository, CancellationToken cancellationToken)
        {
            var repo = await GetAsync<GithubRepository>($"repos/{owner}/{repository}", cancellationToken);
            return repo.DefaultBranch;
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
        public async Task<(string latestTag, string latestSemVerCompliantTag, string latestSemVerCompliantSha)?> GetAvailableUpdatesAsync(string owner, string repository, string tag, CancellationToken cancellationToken)
        {
            if (!VersionHelper.TryParse(tag, out var version))
                throw new NotSupportedException($"Unsupported version tag {tag} (not a parsable version)");

            return await GetLargestSemVerCompliantTagAsync(owner, repository, version, cancellationToken);
        }

        private async Task<(string latestTag, string latestSemVerCompliantTag, string latestSemVerCompliantSha)?> GetLargestSemVerCompliantTagAsync(string owner, string repository, Version currentVersion, CancellationToken cancellationToken)
        {
            var semVerCompliant = new List<TagContainer>();
            Version? max = null;
            string? maxTag = null;
            await foreach (var gitRef in GetPaginatedAsync<GitRef>($"repos/{owner}/{repository}/git/refs/tags", cancellationToken))
            {
                var tag = gitRef.Ref.Substring("refs/tags/".Length);

                // tag must be in format "v1" or "v1.0" ignore all others
                if (!VersionHelper.TryParse(tag, out var tagVersion))
                    continue;

                if (max == null || tagVersion > max)
                {
                    maxTag = tag;
                    max = tagVersion;
                }

                if (tagVersion.Major != currentVersion.Major)
                    continue;

                semVerCompliant.Add(new TagContainer(gitRef, tagVersion, tag));
            }
            if (!semVerCompliant.Any())
                return null; // would be quite problematic in most cases as no version exists anymore

            var maxVersion = maxTag ?? throw new InvalidProgramException("compiler");

            // response order reflects tag creation date NOT semVer order
            var latest = semVerCompliant.OrderByDescending(x => x.Version).First();

            // "v1" type tag may not necessarily exist
            // if it does we prefer it as it is recommended in the documentation 
            var major = semVerCompliant.SingleOrDefault(r => r.Tag.Equals($"v{currentVersion.Major}", StringComparison.OrdinalIgnoreCase));

            // both tags may point to the same commit
            if (major == null ||
                // however SHA can only be identical when both tags are lightweight and point to the same commit
                major.GitRef.Object.Sha == latest.GitRef.Object.Sha)
                return (maxVersion, (major ?? latest).Tag, latest.GitRef.Object.Sha);

            // one (or both) tags may be regular tags (with their own sha)
            // in which case we need to resolve the underlying commit sha to compare

            var majorCommit = await GetCommitAsync(owner, repository, major.GitRef, cancellationToken);
            var latestCommit = await GetCommitAsync(owner, repository, latest.GitRef, cancellationToken);

            // if both point to the same commit or the major format points to a newer commit we pick the major as refernece
            if (majorCommit.sha == latestCommit.sha ||
                // TODO: possibly buggy because git commit creation date can be changed
                // however accept the edgecase as "not supported" as it would require
                // someone to purposefully create a newer commit with an older date..
                majorCommit.createdAt > latestCommit.createdAt)
            {
                return (maxVersion, major.Tag, majorCommit.sha);
            }

            return (maxVersion, latest.Tag, latestCommit.sha);
        }

        private async Task<(string sha, DateTimeOffset createdAt)> GetCommitAsync(string owner, string repository, GitRef gitRef, CancellationToken cancellationToken)
        {
            switch (gitRef.Object.Type)
            {
                case "commit":
                    var commit = await GetAsync<GithubCommit>(gitRef.Object.Url, cancellationToken).ConfigureAwait(false);
                    return (gitRef.Object.Sha, commit.Author.Date);
                case "tag":
                    // type tag is not a lightweight tag -> it contains the link to the actual commit
                    var tag = await GetAsync<GithubTag>(gitRef.Object.Url, cancellationToken).ConfigureAwait(false);
                    // resolve actual commit
                    return await GetCommitAsync(owner, repository, new GitRef
                    {
                        Object = tag.Object
                    }, cancellationToken).ConfigureAwait(false);
                default:
                    throw new NotSupportedException($"Expected a tag to resolve its commit. {gitRef.Object.Type} is unuspported.");
            }
        }

        public async Task<T> GetAsync<T>(string url, CancellationToken cancellationToken)
        {
            var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            CheckRateLimit(response);
            response.EnsureSuccessStatusCode();
            return await JsonSerializer.DeserializeAsync<T>(await response.Content.ReadAsStreamAsync().ConfigureAwait(false)).ConfigureAwait(false);
        }

        /// <summary>
        /// Fetches results from an api endpoint and if said endpoint returns
        /// RFC'd next links then keeps following them (and aggregating the results).
        /// https://www.w3.org/wiki/LinkHeader
        /// Expects the response to be of type array.
        /// </summary>
        private IAsyncEnumerable<T> GetPaginatedAsync<T>(string url, CancellationToken cancellationToken)
            => GetPaginatedAsync(url, e => JsonSerializer.Deserialize<T[]>(e.GetRawText()), cancellationToken);

        private async IAsyncEnumerable<T> GetPaginatedAsync<T>(string url, Func<JsonElement, T[]> map, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            string? nextLink = null;
            do
            {
                var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
                CheckRateLimit(response);
                response.EnsureSuccessStatusCode();
                var results = map(await JsonSerializer.DeserializeAsync<JsonElement>(
                    await response.Content.ReadAsStreamAsync().ConfigureAwait(false),
                    cancellationToken: cancellationToken).ConfigureAwait(false));

                foreach (var r in results)
                    yield return r;

                if (response.Headers.TryGetValues("Link", out var values))
                {
                    nextLink = values
                        .Select(v =>
                        {
                            // content as per spec: https://developer.github.com/v3/#link-header
                            // Link: <https://api.github.com/user/repos?page=3&per_page=100>; rel="next", < https://api.github.com/user/repos?page=50&per_page=100>; rel="last", ...
                            // only care for rel="next" link

                            if (string.IsNullOrEmpty(v) ||
                                !v.Contains(","))
                                return null;

                            foreach (var hyperlinkSections in v.Split(','))
                            {
                                if (!hyperlinkSections.Contains(";"))
                                    continue;
                                var parts = hyperlinkSections.Split(';');
                                if (!parts[1].Trim().Equals("rel=\"next\"", StringComparison.OrdinalIgnoreCase))
                                    continue;

                                return parts[0].Trim().TrimStart('<').TrimEnd('>');
                            }
                            return null;
                        })
                        .Where(x => x != null)
                        .FirstOrDefault();
                    if (nextLink != null)
                    {
                        url = nextLink;
                        if (url.StartsWith(_httpClient.BaseAddress.ToString(), StringComparison.OrdinalIgnoreCase))
                            url = url.Substring(_httpClient.BaseAddress.ToString().Length);
                    }
                }
            }
            while (nextLink != null);
        }

        private void CheckRateLimit(HttpResponseMessage response)
        {
            if (response.StatusCode != System.Net.HttpStatusCode.Forbidden)
                return;

            var limitString = response.Headers.GetValues("X-Ratelimit-Limit").FirstOrDefault();
            var remainingString = response.Headers.GetValues("X-Ratelimit-Remaining").FirstOrDefault();
            var resetString = response.Headers.GetValues("X-Ratelimit-Reset").FirstOrDefault();
            var isAuthenticated = _httpClient.DefaultRequestHeaders.Authorization != null;
            if (!int.TryParse(limitString, out int limit) ||
               !int.TryParse(remainingString, out int remaining) ||
               !long.TryParse(resetString, out long reset))
            {
                throw new GithubApiRatelimitExceededException(0, 0, DateTimeOffset.MaxValue, isAuthenticated, $"Failed to parse rate limit response. Received: Limit: {limitString}, Remaining: {remainingString}, Reset time (unix): {resetString}. See https://developer.github.com/v3/#rate-limiting for details.");
            }
            var dto = DateTimeOffset.FromUnixTimeSeconds(reset);
            throw new GithubApiRatelimitExceededException(remaining, limit, dto, isAuthenticated, $"Github api rate limit has been exceeded ({remaining}/{limit} api calls remaining), will reset {dto}. See https://developer.github.com/v3/#rate-limiting for details.");
        }

        private class TagContainer
        {
            public TagContainer(GitRef gitRef, Version v, string tag)
            {
                GitRef = gitRef;
                Version = v;
                Tag = tag;
            }

            public GitRef GitRef { get; set; }

            public Version Version { get; set; }

            public string Tag { get; set; }
        }
    }
}
