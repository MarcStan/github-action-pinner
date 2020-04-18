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
    /// <summary>
    /// Helper that caches github API responses in memory to prevent multiple API hits.
    /// </summary>
    public class CachedGithubApi
    {
        private readonly HttpClient _httpClient;

        public CachedGithubApi(string? oauthToken)
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

        public async Task<HttpResponseMessage> GetAsync(string url, CancellationToken cancellationToken)
        {
            var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            CheckRateLimit(response);
            return response;
        }

        /// <summary>
        /// Fetches results from an api endpoint and if said endpoint returns
        /// RFC'd next links then keeps following them (and aggregating the results).
        /// https://www.w3.org/wiki/LinkHeader
        /// Expects the response to be of type array.
        /// </summary>
        public IAsyncEnumerable<T> GetPaginatedAsync<T>(string url, CancellationToken cancellationToken)
            => GetPaginatedAsync(url, e => JsonSerializer.Deserialize<T[]>(e.GetRawText()), cancellationToken);

        private async IAsyncEnumerable<T> GetPaginatedAsync<T>(string url, Func<JsonElement, T[]> map, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            string? nextLink = null;
            do
            {
                var response = await GetAsync(url, cancellationToken).ConfigureAwait(false);
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
    }
}
