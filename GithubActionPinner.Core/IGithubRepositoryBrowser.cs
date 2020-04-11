using System.Threading;
using System.Threading.Tasks;

namespace GithubActionPinner.Core
{
    public interface IGithubRepositoryBrowser
    {
        /// <summary>
        /// Will return whether the repository is accessible to the current user.
        /// If no token is used, then only public repositories are accessible.
        /// With a token all repositories with read access are considered accessible.
        /// </summary>
        Task<bool> IsRepositoryAccessibleAsync(string owner, string repository, CancellationToken cancellationToken);

        /// <summary>
        /// For a given branch name gets the SHA of the latest commit on it.
        /// </summary>
        Task<string> GetShaForLatestCommitAsync(string owner, string repository, string branchName, CancellationToken cancellationToken);

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
        Task<(string tag, string sha)?> GetLatestSemVerCompliantAsync(string owner, string repository, string tag, CancellationToken cancellationToken);
    }
}
