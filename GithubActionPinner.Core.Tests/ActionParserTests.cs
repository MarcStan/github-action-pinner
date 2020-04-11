using GithubActionPinner.Core;
using GithubActionPinner.Core.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

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
        [DataRow("  - uses: actions/foo@de4cd7198fed4a740bdc2073abeb76e496c7c6fe # random comment", "actions/foo", ActionReferenceType.Sha, "de4cd7198fed4a740bdc2073abeb76e496c7c6fe", "actions", "foo", "random comment")]
        public void ValidActionShouldParseSuccessfully(string reference, string name, ActionReferenceType type, string version, string owner, string repository, string comment)
        {
            var parser = new ActionParser();
            var r = parser.ParseAction(reference);
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
        public void PinnedActionShouldParseSuccessfully(string reference, string name, ActionReferenceType type, string version, string owner, string repository, string comment, ActionReferenceType pinnedType, string pinnedVersion)
        {
            var parser = new ActionParser();
            var r = parser.ParseAction(reference);
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
        // TODO: default branch can be changed on github
        [DataRow("  - uses: actions/foo", "actions/foo", ActionReferenceType.Branch, "master", "actions", "foo", "")]
        public void ActionWithoutReferenceShouldParseSuccessfully(string reference, string name, ActionReferenceType type, string version, string owner, string repository, string comment)
        {
            var parser = new ActionParser();
            var r = parser.ParseAction(reference);
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
        public void UnsupportedActionsShouldThrow(string reference)
        {
            var parser = new ActionParser();
            try
            {
                parser.ParseAction(reference);
                Assert.Fail("should throw");
            }
            catch (NotSupportedException)
            {
            }
        }
    }
}
