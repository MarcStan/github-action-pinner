using GithubActionPinner.Core.Config;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GithubActionPinner.Core.Tests
{
    [TestClass]
    public class ActionConfigTests
    {
        [TestMethod]
        public void NoConfigFileShouldNotThrow()
        {
            var config = new ActionConfig();
            Assert.IsFalse(config.IsRepositoryTrusted("actions", "checkout"));
            Assert.IsFalse(config.IsCommitAudited("actions", "checkout", "01aecccf739ca6ff86c0539fbc67a7a5007bbc81"));
        }

        [TestMethod]
        public void EmptyConfigFileShouldNotThrow()
        {
            using var data = FileHelper.ExtractDataFileTemporarily("trustNoOne.trusted");

            var config = new ActionConfig();
            config.Load(data.FilePath);
            Assert.IsFalse(config.IsRepositoryTrusted("actions", "checkout"));
            Assert.IsFalse(config.IsCommitAudited("actions", "checkout", "01aecccf739ca6ff86c0539fbc67a7a5007bbc81"));
        }

        [TestMethod]
        public void TrustedOrgShouldApplyToAllRepos()
        {
            using var data = FileHelper.ExtractDataFileTemporarily("trustOrg.trusted");

            var config = new ActionConfig();
            config.Load(data.FilePath);
            Assert.IsTrue(config.IsRepositoryTrusted("actions", "checkout"));
            Assert.IsFalse(config.IsCommitAudited("actions", "checkout", "01aecccf739ca6ff86c0539fbc67a7a5007bbc81"));
        }

        [TestMethod]
        public void TrustedOrgShouldNotApplyToOtherOrg()
        {
            using var data = FileHelper.ExtractDataFileTemporarily("trustOrg.trusted");

            var config = new ActionConfig();
            config.Load(data.FilePath);

            Assert.IsFalse(config.IsRepositoryTrusted("microsoft", "checkout"));
            Assert.IsFalse(config.IsCommitAudited("microsoft", "checkout", "01aecccf739ca6ff86c0539fbc67a7a5007bbc81"));
        }

        [TestMethod]
        public void TrustedRepoShouldNotApplyToOtherRepos()
        {
            using var data = FileHelper.ExtractDataFileTemporarily("trustRepo.trusted");

            var config = new ActionConfig();
            config.Load(data.FilePath);
            Assert.IsTrue(config.IsRepositoryTrusted("actions", "checkout"));
            Assert.IsFalse(config.IsRepositoryTrusted("actions", "checkout-v2"));
            Assert.IsFalse(config.IsRepositoryTrusted("microsoft", "checkout"));
            Assert.IsFalse(config.IsCommitAudited("actions", "checkout", "01aecccf739ca6ff86c0539fbc67a7a5007bbc81"));
            Assert.IsFalse(config.IsCommitAudited("action", "checkout", "01aecccf739ca6ff86c0539fbc67a7a5007bbc81"));
        }

        [TestMethod]
        public void TrustedCommitsShouldOnlyApplyToRepo()
        {
            using var data = FileHelper.ExtractDataFileTemporarily("trustCommit.trusted");

            var config = new ActionConfig();
            config.Load(data.FilePath);
            Assert.IsFalse(config.IsRepositoryTrusted("actions", "checkout"));
            Assert.IsTrue(config.IsCommitAudited("actions", "checkout", "01aecccf739ca6ff86c0539fbc67a7a5007bbc81"));
            Assert.IsFalse(config.IsCommitAudited("action", "checkout", "01aecccf739ca6ff86c0539fbc67a7a5007bbc81"));
            Assert.IsFalse(config.IsCommitAudited("actions", "checkout2", "01aecccf739ca6ff86c0539fbc67a7a5007bbc81"));
            Assert.IsFalse(config.IsCommitAudited("foo", "bar", "01aecccf739ca6ff86c0539fbc67a7a5007bbc81"));
            Assert.IsFalse(config.IsCommitAudited("foo", "bar", "111ecccf739ca6ff86c0539fbc67a7a5007bbc81"));
        }

        [TestMethod]
        public void MixedConfig()
        {
            using var data = FileHelper.ExtractDataFileTemporarily("trustSome.trusted");

            var config = new ActionConfig();
            config.Load(data.FilePath);
            Assert.IsFalse(config.IsRepositoryTrusted("actions", "checkout"));
            Assert.IsFalse(config.IsRepositoryTrusted("actions", "somethingelse"));
            Assert.IsTrue(config.IsCommitAudited("actions", "checkout", "01aecccf739ca6ff86c0539fbc67a7a5007bbc81"));
            Assert.IsTrue(config.IsRepositoryTrusted("microsoft", "anything"));
            Assert.IsFalse(config.IsRepositoryTrusted("nuget", "foo"));
            Assert.IsTrue(config.IsRepositoryTrusted("nuget", "setup-nuget"));
        }
    }
}
