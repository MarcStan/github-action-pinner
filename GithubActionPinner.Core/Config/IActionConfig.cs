namespace GithubActionPinner.Core.Config
{
    public interface IActionConfig
    {
        void Load(string configFile);

        bool IsCommitAudited(string owner, string repo, string sha);

        bool IsRepositoryTrusted(string owner, string repo);
    }
}
