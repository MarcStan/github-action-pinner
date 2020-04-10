using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GithubActionPinner.Core.Extensions
{
    public static class HttpClientExtensions
    {
        public static async Task<T> GetAsync<T>(this HttpClient httpClient, string url, CancellationToken cancellationToken)
        {
            var response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return await JsonSerializer.DeserializeAsync<T>(await response.Content.ReadAsStreamAsync().ConfigureAwait(false)).ConfigureAwait(false);
        }

        /// <summary>
        /// Fetches results from an api endpoint and if said endpoint returns
        /// RFC'd next links then keeps following them (and aggregating the results).
        /// https://www.w3.org/wiki/LinkHeader
        /// Expects the response to be of type array.
        /// </summary>
        public static IAsyncEnumerable<T> GetPaginatedAsync<T>(this HttpClient httpClient, string url, CancellationToken cancellationToken)
            => GetPaginatedAsync(httpClient, url, e => JsonSerializer.Deserialize<T[]>(e.GetRawText()), cancellationToken);

        /// <summary>
        /// Fetches results from an api endpoint and if said endpoint returns
        /// RFC'd next links then keeps following them (and aggregating the results).
        /// https://www.w3.org/wiki/LinkHeader
        /// Expects the to be an object with a property of type array (e.g. { total: 7, values: "" }
        /// </summary>
        /// <param name="propertyName">The name of the property where the array is stored, in the example it would be "values".</param>
        public static IAsyncEnumerable<T> GetPaginatedAsync<T>(this HttpClient httpClient, string url, string propertyName, CancellationToken cancellationToken)
            => GetPaginatedAsync(httpClient, url, e => JsonSerializer.Deserialize<T[]>(e.GetProperty(propertyName).GetRawText()), cancellationToken);

        private static async IAsyncEnumerable<T> GetPaginatedAsync<T>(this HttpClient httpClient, string url, Func<JsonElement, T[]> map, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            string? nextLink = null;
            do
            {
                var response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
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
                        if (url.StartsWith(httpClient.BaseAddress.ToString(), StringComparison.OrdinalIgnoreCase))
                            url = url.Substring(httpClient.BaseAddress.ToString().Length);
                    }
                }
            }
            while (nextLink != null);
        }
    }
}
