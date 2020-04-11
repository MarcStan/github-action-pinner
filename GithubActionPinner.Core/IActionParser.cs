using GithubActionPinner.Core.Models;
using System.Threading;
using System.Threading.Tasks;

namespace GithubActionPinner.Core
{
    public interface IActionParser
    {
        Task<ActionReference> ParseActionAsync(string text, CancellationToken cancellationToken);
    }
}
