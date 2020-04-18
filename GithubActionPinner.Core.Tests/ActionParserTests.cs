using GithubActionPinner.Core;
using GithubActionPinner.Core.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace GithubActionStats.Core.Tests
{
    [TestClass]
    public class ActionParserTests
    {
        [DataTestMethod]
        [DataRow("  - uses: actions/foo@v1", "actions/foo", ActionReferenceType.Tag, "v1", "actions", "foo", "")]
        [DataRow("- uses: actions/foo@v2", "actions/foo", ActionReferenceType.Tag, "v2", "actions", "foo", "")]
        [DataRow("          \t         \t- uses: actions/foo@v2          \t              ", "actions/foo", ActionReferenceType.Tag, "v2", "actions", "foo", "")]
        [DataRow("  - uses: actions/foo@v1.1", "actions/foo", ActionReferenceType.Tag, "v1.1", "actions", "foo", "")]
        [DataRow("  - uses: actions/foo@master", "actions/foo", ActionReferenceType.Branch, "master", "actions", "foo", "")]
        [DataRow("  - uses: actions/foo@dev", "actions/foo", ActionReferenceType.Branch, "dev", "actions", "foo", "")]
        [DataRow("  - uses: actions/repo/subdir@v1", "actions/repo/subdir", ActionReferenceType.Tag, "v1", "actions", "repo", "")]
        [DataRow("  - uses: actions/repo/dir1/dir2@dev", "actions/repo/dir1/dir2", ActionReferenceType.Branch, "dev", "actions", "repo", "")]
        [DataRow("  - uses: actions/foo@de4cd7198fed4a740bdc2073abeb76e496c7c6fe", "actions/foo", ActionReferenceType.Sha, "de4cd7198fed4a740bdc2073abeb76e496c7c6fe", "actions", "foo", "")]
        [DataRow("  - uses: actions/foo@de4cd7198fed4a740bdc2073abeb76e496c7c6fe # random comment\t", "actions/foo", ActionReferenceType.Sha, "de4cd7198fed4a740bdc2073abeb76e496c7c6fe", "actions", "foo", " random comment\t")]
        public async Task ValidActionShouldParseSuccessfully(string reference, string name, ActionReferenceType type, string version, string owner, string repository, string comment)
        {
            var repoBrowser = new Mock<IGithubRepositoryBrowser>();
            repoBrowser
                .Setup(x => x.GetRepositoryDefaultBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult("nope"));

            var parser = new ActionParser(repoBrowser.Object);
            var r = await parser.ParseActionAsync(reference, CancellationToken.None);
            Assert.AreEqual(name, r.ActionName);
            Assert.AreEqual(type, r.ReferenceType);
            Assert.AreEqual(version, r.ReferenceVersion);
            Assert.AreEqual(owner, r.Owner);
            Assert.AreEqual(repository, r.Repository);
            Assert.AreEqual(comment, r.Comment);

            Assert.IsNull(r.Pinned);
        }

        [DataTestMethod]
        [DataRow("  - uses: actions/foo@de4cd7198fed4a740bdc2073abeb76e496c7c6fe # pin@v1", "actions/foo", ActionReferenceType.Sha, "de4cd7198fed4a740bdc2073abeb76e496c7c6fe", "actions", "foo", "", ActionReferenceType.Tag, "v1")]
        [DataRow("  - uses: actions/foo@de4cd7198fed4a740bdc2073abeb76e496c7c6fe # pin@master random comment", "actions/foo", ActionReferenceType.Sha, "de4cd7198fed4a740bdc2073abeb76e496c7c6fe", "actions", "foo", "random comment", ActionReferenceType.Branch, "master")]
        [DataRow("  - uses: actions/foo@de4cd7198fed4a740bdc2073abeb76e496c7c6fe # pin@master random comment   ", "actions/foo", ActionReferenceType.Sha, "de4cd7198fed4a740bdc2073abeb76e496c7c6fe", "actions", "foo", "random comment   ", ActionReferenceType.Branch, "master")]
        [DataRow("  - uses: actions/foo@de4cd7198fed4a740bdc2073abeb76e496c7c6fe # pin@master  random comment   ", "actions/foo", ActionReferenceType.Sha, "de4cd7198fed4a740bdc2073abeb76e496c7c6fe", "actions", "foo", " random comment   ", ActionReferenceType.Branch, "master")]
        public async Task PinnedActionShouldParseSuccessfully(string reference, string name, ActionReferenceType type, string version, string owner, string repository, string comment, ActionReferenceType pinnedType, string pinnedVersion)
        {
            var repoBrowser = new Mock<IGithubRepositoryBrowser>();
            repoBrowser
                .Setup(x => x.GetRepositoryDefaultBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult("nope"));

            var parser = new ActionParser(repoBrowser.Object);
            var r = await parser.ParseActionAsync(reference, CancellationToken.None);
            Assert.AreEqual(name, r.ActionName);
            Assert.AreEqual(type, r.ReferenceType);
            Assert.AreEqual(version, r.ReferenceVersion);
            Assert.AreEqual(owner, r.Owner);
            Assert.AreEqual(repository, r.Repository);
            Assert.AreEqual(comment, r.Comment);

            Assert.IsNotNull(r.Pinned);
            Assert.AreEqual(pinnedType, r.Pinned.ReferenceType);
            Assert.AreEqual(pinnedVersion, r.Pinned.ReferenceVersion);
        }

        [DataTestMethod]
        [DataRow("  - uses: actions/foo", "actions/foo", "master", ActionReferenceType.Branch, "master", "actions", "foo", "")]
        [DataRow("  - uses: actions/foo", "actions/foo", "dev", ActionReferenceType.Branch, "dev", "actions", "foo", "")]
        public async Task ActionWithoutReferenceShouldParseSuccessfully(string reference, string name, string defaultBranch, ActionReferenceType type, string version, string owner, string repository, string comment)
        {
            var repoBrowser = new Mock<IGithubRepositoryBrowser>();
            repoBrowser
                .Setup(x => x.GetRepositoryDefaultBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(defaultBranch));

            var parser = new ActionParser(repoBrowser.Object);
            var r = await parser.ParseActionAsync(reference, CancellationToken.None);
            Assert.AreEqual(name, r.ActionName);
            Assert.AreEqual(type, r.ReferenceType);
            Assert.AreEqual(version, r.ReferenceVersion);
            Assert.AreEqual(owner, r.Owner);
            Assert.AreEqual(repository, r.Repository);
            Assert.AreEqual(comment, r.Comment);
        }

        [DataTestMethod]
        [DataRow("  - uses: actions")]
        [DataRow("  - uses: docker://foo")]
        [DataRow("  - uses: ./local/foo")]
        public async Task UnsupportedActionsShouldThrow(string reference)
        {
            var repoBrowser = new Mock<IGithubRepositoryBrowser>();
            repoBrowser
                .Setup(x => x.GetRepositoryDefaultBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult("nope"));

            var parser = new ActionParser(repoBrowser.Object);
            try
            {
                await parser.ParseActionAsync(reference, CancellationToken.None);
                Assert.Fail("should throw");
            }
            catch (NotSupportedException)
            {
            }
            repoBrowser.VerifyNoOtherCalls();
        }

        [DataTestMethod]
        [DataRow("#  - uses: actions/foo@v1")]
        [DataRow("#- uses: actions/foo@v2")]
        [DataRow("  #- uses: actions/foo@v2")]
        public async Task CommentLinesShouldBeIgnored(string reference)
        {
            var repoBrowser = new Mock<IGithubRepositoryBrowser>();
            repoBrowser
                .Setup(x => x.GetRepositoryDefaultBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult("nope"));

            var parser = new ActionParser(repoBrowser.Object);
            try
            {
                await parser.ParseActionAsync(reference, CancellationToken.None);
                Assert.Fail();
            }
            catch (NotSupportedException)
            {
            }
        }
    }
}
