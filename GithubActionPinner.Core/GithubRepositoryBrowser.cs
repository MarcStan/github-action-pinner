using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GithubActionPinner.Core
{
    public class GithubRepositoryBrowser : IGithubRepositoryBrowser
    {
        public Task<bool> IsPublicAsync(string repository, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// For a given branch name gets the SHA of the latest commit on it.
        /// </summary>
        public Task<string> GetShaForLatestCommitAsync(string repository, string branchName, CancellationToken cancellationToken)
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
        public async Task<(string tag, string sha)?> GetShaForLatestSemVerCompliantCommitAsync(string repository, string tag, CancellationToken cancellationToken)
        {
            if (!tag.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                throw new NotSupportedException($"Unsupported version tag {tag}");

            // format "v1" is mainly used to reference github Actions but is not compatible with .Net Version so parse to "1.0"
            if (!Version.TryParse(int.TryParse(tag.Substring(1), out int major) ? $"{major}.0" : tag.Substring(1), out var version))
                throw new NotSupportedException($"Unsupported version tag {tag} (not a parsable version)");

            var versions = await GetLargerSemVerCompliantTagsAsync(version, cancellationToken);
            if (versions.Any())
                return versions[0];

            return null;
        }

        private Task<(string tag, string sha)[]> GetLargerSemVerCompliantTagsAsync(Version version, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }
    }
}
