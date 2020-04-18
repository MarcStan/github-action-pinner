using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;

namespace GithubActionPinner.Core.Models
{
    /// <summary>
    /// A reusable response object from a http call.
    /// </summary>
    public class HttpResponse
    {
        public HttpResponse(HttpStatusCode code, HttpResponseHeaders headers, Stream content)
        {
            StatusCode = code;
            Headers = headers;
            Content = content;
        }

        public HttpStatusCode StatusCode { get; set; }

        public HttpResponseHeaders Headers { get; set; }

        public Stream Content { get; set; }

        public void EnsureSuccessStatusCode()
        {
            if (StatusCode != HttpStatusCode.OK)
                throw new HttpRequestException($"Received {StatusCode}");
        }
    }
}
