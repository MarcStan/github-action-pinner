using GithubActionPinner.Core;
using GithubActionPinner.Core.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GithubActionStats.Core.Tests
{
    [TestClass]
    public class ActionParserTests
    {
        [DataTestMethod]
        [DataRow("  - uses: action/foo@v1", "action/foo", ActionReferenceType.Tag, "v1", "")]
        [DataRow("  - uses: action/foo@v2", "action/foo", ActionReferenceType.Tag, "v2", "")]
        [DataRow("  - uses: action/foo@v1.1", "action/foo", ActionReferenceType.Tag, "v1.1", "")]
        [DataRow("  - uses: action/foo@master", "action/foo", ActionReferenceType.Branch, "master", "")]
        [DataRow("  - uses: action/foo@dev", "action/foo", ActionReferenceType.Branch, "dev", "")]
        [DataRow("  - uses: action/subdir/foo@v1", "action/subdir/foo", ActionReferenceType.Tag, "v1", "")]
        [DataRow("  - uses: action/subdir/dir2/foo@dev", "action/subdir/dir2/foo", ActionReferenceType.Branch, "dev", "")]
        [DataRow("  - uses: action/foo@de4cd7198fed4a740bdc2073abeb76e496c7c6fe", "action/foo", ActionReferenceType.Sha, "de4cd7198fed4a740bdc2073abeb76e496c7c6fe", "")]
        [DataRow("  - uses: action/foo@de4cd7198fed4a740bdc2073abeb76e496c7c6fe # @v1", "action/foo", ActionReferenceType.Sha, "de4cd7198fed4a740bdc2073abeb76e496c7c6fe", "@v1")]
        [DataRow("  - uses: action/foo@de4cd7198fed4a740bdc2073abeb76e496c7c6fe # random comment", "action/foo", ActionReferenceType.Sha, "de4cd7198fed4a740bdc2073abeb76e496c7c6fe", "random comment")]
        public void ValidActionShouldParseSuccessfully(string reference, string name, ActionReferenceType type, string version, string comment)
        {
            var parser = new ActionParser();
            var r = parser.ParseAction(reference);
            Assert.AreEqual(name, r.ActionName);
            Assert.AreEqual(type, r.ReferenceType);
            Assert.AreEqual(version, r.ReferenceVersion);
            Assert.AreEqual(comment, r.Comment);
        }
    }
}
