using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace GithubActionPinner.Core.Tests
{
    [TestClass]
    public class VersionHelperTests
    {
        [DataTestMethod]
        [DataRow("v1", "1.0")]
        [DataRow("v1.0", "1.0")]
        [DataRow("v1.0.0", "1.0.0")]
        [DataRow("v1.2.3", "1.2.3")]
        [DataRow("v7", "7.0")]
        [DataRow("v8.1", "8.1")]
        public void ParsingValidVersionShouldSucceed(string text, string version)
        {
            var v = Version.Parse(version);
            Assert.IsTrue(VersionHelper.TryParse(text, out var matched));
            Assert.AreEqual(v, matched);
        }
    }
}
