using GithubActionPinner.Core;
using GithubActionPinner.Core.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GithubActionStats.Core.Tests
{
    [TestClass]
    public class ActionParserTests
    {
        [DataTestMethod]
        [DataRow("  - uses: action/foo@v1", "action/foo", ActionReferenceType.Tag, "v1", "action/foo", "")]
        [DataRow("- uses: action/foo@v2", "action/foo", ActionReferenceType.Tag, "v2", "action/foo", "")]
        [DataRow("          \t         \t- uses: action/foo@v2          \t              ", "action/foo", ActionReferenceType.Tag, "v2", "action/foo", "")]
        [DataRow("  - uses: action/foo@v1.1", "action/foo", ActionReferenceType.Tag, "v1.1", "action/foo", "")]
        [DataRow("  - uses: action/foo@master", "action/foo", ActionReferenceType.Branch, "master", "action/foo", "")]
        [DataRow("  - uses: action/foo@dev", "action/foo", ActionReferenceType.Branch, "dev", "action/foo", "")]
        [DataRow("  - uses: action/repo/subdir@v1", "action/repo/subdir", ActionReferenceType.Tag, "v1", "action/repo", "")]
        [DataRow("  - uses: action/repo/dir1/dir2@dev", "action/repo/dir1/dir2", ActionReferenceType.Branch, "dev", "action/repo", "")]
        [DataRow("  - uses: action/foo@de4cd7198fed4a740bdc2073abeb76e496c7c6fe", "action/foo", ActionReferenceType.Sha, "de4cd7198fed4a740bdc2073abeb76e496c7c6fe", "action/foo", "")]
        [DataRow("  - uses: action/foo@de4cd7198fed4a740bdc2073abeb76e496c7c6fe # @v1", "action/foo", ActionReferenceType.Sha, "de4cd7198fed4a740bdc2073abeb76e496c7c6fe", "action/foo", "@v1")]
        [DataRow("  - uses: action/foo@de4cd7198fed4a740bdc2073abeb76e496c7c6fe # random comment", "action/foo", ActionReferenceType.Sha, "de4cd7198fed4a740bdc2073abeb76e496c7c6fe", "action/foo", "random comment")]
        public void ValidActionShouldParseSuccessfully(string reference, string name, ActionReferenceType type, string version, string repository, string comment)
        {
            var parser = new ActionParser();
            var r = parser.ParseAction(reference);
            Assert.AreEqual(name, r.ActionName);
            Assert.AreEqual(type, r.ReferenceType);
            Assert.AreEqual(version, r.ReferenceVersion);
            Assert.AreEqual(repository, r.Repository);
            Assert.AreEqual(comment, r.Comment);
        }
    }
}
