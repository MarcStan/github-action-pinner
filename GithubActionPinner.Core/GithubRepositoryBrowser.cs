using GithubActionPinner.Core.Models.Github;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GithubActionPinner.Core
{
    public class GithubRepositoryBrowser : IGithubRepositoryBrowser
    {
        private readonly CachedGithubApi _cache;

        public GithubRepositoryBrowser(CachedGithubApi cache)
            => _cache = cache;

        public async Task<bool> IsRepositoryAccessibleAsync(string owner, string repository, CancellationToken cancellationToken)
        {
            var response = await _cache.GetAsync($"repos/{owner}/{repository}", cancellationToken).ConfigureAwait(false);
            return response.StatusCode == HttpStatusCode.OK;
        }

        public async Task<string> GetRepositoryDefaultBranchAsync(string owner, string repository, CancellationToken cancellationToken)
        {
            var repo = await GetAsync<GithubRepository>($"repos/{owner}/{repository}", cancellationToken).ConfigureAwait(false);
            return repo.DefaultBranch;
        }

        /// <summary>
        /// For a given branch name gets the SHA of the latest commit on it.
        /// </summary>
        public async Task<string?> GetShaForLatestCommitAsync(string owner, string repository, string branchName, CancellationToken cancellationToken)
        {
            try
            {
                var branchInfo = await GetAsync<BranchInfo>($"repos/{owner}/{repository}/branches/{branchName}", cancellationToken).ConfigureAwait(false);
                return branchInfo.Commit.Sha;
            }
            catch (HttpRequestException)
            {
                return null;
            }
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

            return await GetLargestSemVerCompliantTagAsync(owner, repository, version, cancellationToken).ConfigureAwait(false);
        }

        private async Task<T> GetAsync<T>(string url, CancellationToken cancellationToken) where T : new()
        {
            var response = await _cache.GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            return await JsonSerializer.DeserializeAsync<T>(response.Content).ConfigureAwait(false) ?? new T();
        }

        private async Task<(string latestTag, string latestSemVerCompliantTag, string latestSemVerCompliantSha)?> GetLargestSemVerCompliantTagAsync(string owner, string repository, Version currentVersion, CancellationToken cancellationToken)
        {
            var semVerCompliant = new List<TagContainer>();
            Version? max = null;
            string? maxTag = null;
            await foreach (var gitRef in _cache.GetPaginatedAsync<GitRef>($"repos/{owner}/{repository}/git/refs/tags", cancellationToken))
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
            if (semVerCompliant.Count == 0)
                return null; // would be quite problematic in most cases as no version exists anymore, just ignore as we can't update

            var maxVersion = maxTag ?? throw new InvalidProgramException("compiler");

            // response order reflects tag creation date NOT semVer order
            var latestCommitByVersion = semVerCompliant.OrderByDescending(x => x.Version).First();

            // "v1" type tag may not necessarily exist
            // if it does we prefer it as it is recommended in the documentation 
            var majorTag = semVerCompliant.SingleOrDefault(r => r.Tag.Equals($"v{currentVersion.Major}", StringComparison.OrdinalIgnoreCase));

            // both tags may point to the same commit
            if (majorTag == null ||
                // however SHA can only be identical when both tags are lightweight and point to the same commit
                majorTag.GitRef.Object.Sha == latestCommitByVersion.GitRef.Object.Sha)
            {
                var latestSha = await GetCommitShaAsync(owner, repository, latestCommitByVersion.GitRef, cancellationToken).ConfigureAwait(false);
                return (maxVersion, (majorTag ?? latestCommitByVersion).Tag, latestSha);
            }

            // one (or both) tags may be regular tags (with their own sha)
            // in which case we need to resolve the underlying commit sha to compare

            var (majorSha, majorCreatedAt) = await GetCommitAsync(owner, repository, majorTag.GitRef, cancellationToken).ConfigureAwait(false);
            var (LatestSha, latestCreatedAt) = await GetCommitAsync(owner, repository, latestCommitByVersion.GitRef, cancellationToken).ConfigureAwait(false);

            if (majorSha == LatestSha ||
                // TODO: possibly buggy because git commit creation date can be changed
                // however accept the edgecase as "not supported" as it would require
                // someone to purposefully create a newer commit with an older date..
                majorCreatedAt > latestCreatedAt)
            {
                // if both point to the same commit or the major format points to a newer commit we pick major
                // Github also recommends users to point the major tag at any semver compliant latest tag
                // so this will give the latest compatible version
                return (majorTag.Tag, majorTag.Tag, majorSha);
            }

            return (maxVersion, latestCommitByVersion.Tag, LatestSha);
        }

        private async Task<string> GetCommitShaAsync(string owner, string repository, GitRef gitRef, CancellationToken cancellationToken)
        {
            switch (gitRef.Object.Type)
            {
                case "commit":
                    // no need to query api again
                    return gitRef.Object.Sha;
                case "tag":
                    // type tag is not a lightweight tag -> it contains the link to the actual commit
                    var tag = await GetAsync<GithubTag>(gitRef.Object.Url, cancellationToken).ConfigureAwait(false);
                    // resolve actual commit
                    return await GetCommitShaAsync(owner, repository, new GitRef
                    {
                        Object = tag.Object
                    }, cancellationToken).ConfigureAwait(false);
                default:
                    throw new NotSupportedException($"Expected a tag to resolve its commit. {gitRef.Object.Type} is unuspported.");
            }
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
