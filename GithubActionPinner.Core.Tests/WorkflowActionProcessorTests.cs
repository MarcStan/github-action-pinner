using GithubActionPinner.Core.Config;
using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace GithubActionPinner.Core.Tests
{
    [TestClass]
    public class WorkflowActionProcessorTests
    {
        [DataTestMethod]
        [DataRow("actions", "checkout", "v2", "v2", "de4cd7198fed4a740bdc2073abeb76e496c7c6fe")]
        [DataRow("actions", "checkout", null, "master", "de4cd7198fed4a740bdc2073abeb76e496c7c6fe")]
        [DataRow("actions", "checkout", "v1", "v1.1", "de4cd7198fed4a740bdc2073abeb76e496c7c6fe")]
        [DataRow("actions", "checkout", "v1", "v1.1.0", "de4cd7198fed4a740bdc2073abeb76e496c7c6fe")]
        public async Task ActionShouldBePinned(string owner, string repo, string currentVersion, string latestVersion, string latestSha)
        {
            var actionName = $"{owner}/{repo}";
            using var tmp = FileHelper.ExtractAndTransformDataFileTemporarily("one-action-transformed.yml", currentVersion == null ? actionName : $"{actionName}@{currentVersion}");

            var repoBrowser = new Mock<IGithubRepositoryBrowser>();
            repoBrowser
                .Setup(x => x.IsRepositoryAccessibleAsync(owner, repo, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));
            (string, string)? response = (latestVersion, latestSha);
            repoBrowser
                .Setup(x => x.GetLatestSemVerCompliantAsync(owner, repo, currentVersion, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(response));
            repoBrowser
                .Setup(x => x.GetShaForLatestCommitAsync(owner, repo, latestVersion, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(latestSha));

            // only makes sense if a branch is referenced
            repoBrowser
                .Setup(x => x.GetRepositoryDefaultBranchAsync(owner, repo, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(latestVersion));

            var parser = new ActionParser(repoBrowser.Object);
            var processor = new WorkflowActionProcessor(repoBrowser.Object, new Mock<IActionConfig>().Object, parser, new Mock<ILogger<WorkflowActionProcessor>>().Object);
            try
            {
                await processor.ProcessAsync(tmp.FilePath, true, CancellationToken.None);
            }
            catch
            {
                Assert.Fail("should not throw");
            }
            var file = File.ReadAllText(tmp.FilePath);
            if (!string.IsNullOrEmpty(currentVersion))
            {
                Assert.IsFalse(file.Contains($"{actionName}@{currentVersion}"));
            }
            else
            {
                // old version had no latest -> can be any text not followed by @
                var regex = new Regex(Regex.Escape(actionName) + "[^@]");
                Assert.IsFalse(regex.IsMatch(file));
            }
            Assert.IsTrue(file.Contains($"{actionName}@{latestSha} # pin@{latestVersion}"));

            repoBrowser.Verify(x => x.IsRepositoryAccessibleAsync(owner, repo, It.IsAny<CancellationToken>()), Times.Once);
            // can be tag or branch
            if (!string.IsNullOrEmpty(currentVersion) && VersionHelper.TryParse(currentVersion, out _))
            {
                repoBrowser.Verify(x => x.GetLatestSemVerCompliantAsync(owner, repo, currentVersion, It.IsAny<CancellationToken>()), Times.Once);
                // tags shouldn't query default branch
                repoBrowser.Verify(x => x.GetRepositoryDefaultBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            }
            else
            {
                repoBrowser.Verify(x => x.GetShaForLatestCommitAsync(owner, repo, latestVersion, It.IsAny<CancellationToken>()), Times.Once);
                repoBrowser.Verify(x => x.GetRepositoryDefaultBranchAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            }
        }

        [DataTestMethod]
        [DataRow("actions", "checkout", "de4cd7198fed4a740bdc2073abeb76e496c7c6fe", "v2", "v2", "de4cd7198fed4a740bdc2073abeb76e496c7c6fe")]
        public async Task PinnedActionShouldBeUpdatedIfPossible(string owner, string repo, string currentSha, string currentVersion, string latestVersion, string latestSha)
        {
            var actionName = $"{owner}/{repo}";
            using var tmp = FileHelper.ExtractAndTransformDataFileTemporarily("one-action-transformed.yml", $"{actionName}@{currentSha} # pin@{currentVersion}");

            var repoBrowser = new Mock<IGithubRepositoryBrowser>();
            repoBrowser
                .Setup(x => x.IsRepositoryAccessibleAsync(owner, repo, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));
            (string, string)? response = (latestVersion, latestSha);
            repoBrowser
                .Setup(x => x.GetLatestSemVerCompliantAsync(owner, repo, currentVersion, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(response));
            repoBrowser
                .Setup(x => x.GetShaForLatestCommitAsync(owner, repo, latestVersion, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(latestSha));

            var parser = new ActionParser(repoBrowser.Object);
            var processor = new WorkflowActionProcessor(repoBrowser.Object, new Mock<IActionConfig>().Object, parser, new Mock<ILogger<WorkflowActionProcessor>>().Object);
            try
            {
                await processor.ProcessAsync(tmp.FilePath, true, CancellationToken.None);
            }
            catch
            {
                Assert.Fail("should not throw");
            }
            var file = File.ReadAllText(tmp.FilePath);
            if (!string.IsNullOrEmpty(currentVersion))
            {
                Assert.IsFalse(file.Contains($"{actionName}@{currentVersion}"));
            }
            else
            {
                // old version had no latest -> can be any text not followed by @
                var regex = new Regex(Regex.Escape(actionName) + "[^@]");
                Assert.IsFalse(regex.IsMatch(file));
            }
            Assert.IsTrue(file.Contains($"{actionName}@{latestSha} # pin@{latestVersion}"));

            repoBrowser.Verify(x => x.IsRepositoryAccessibleAsync(owner, repo, It.IsAny<CancellationToken>()), Times.Once);
            // can be tag or branch
            if (!string.IsNullOrEmpty(currentVersion) && VersionHelper.TryParse(currentVersion, out _))
                repoBrowser.Verify(x => x.GetLatestSemVerCompliantAsync(owner, repo, currentVersion, It.IsAny<CancellationToken>()), Times.Once);
            else
                repoBrowser.Verify(x => x.GetShaForLatestCommitAsync(owner, repo, latestVersion, It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task ValidYmlFileShouldParseSuccessfully()
        {
            using var tmp = FileHelper.ExtractDataFileTemporarily("test.yml");

            var repoBrowser = new Mock<IGithubRepositoryBrowser>();
            repoBrowser
                .Setup(x => x.IsRepositoryAccessibleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));
            var parser = new ActionParser(repoBrowser.Object);
            var processor = new WorkflowActionProcessor(repoBrowser.Object, new Mock<IActionConfig>().Object, parser, new Mock<ILogger<WorkflowActionProcessor>>().Object);
            try
            {
                await processor.ProcessAsync(tmp.FilePath, false, CancellationToken.None);
            }
            catch (Exception)
            {
                Assert.Fail("should not throw");
            }
        }

        [TestMethod]
        public async Task InvalidYmlFileShouldIgnoreInvalidLinesButParseSuccessfully()
        {
            using var tmp = FileHelper.ExtractDataFileTemporarily("invalid-but-parsable.yml");

            var mock = new Mock<ILogger<WorkflowActionProcessor>>();
            var repoBrowser = new Mock<IGithubRepositoryBrowser>();
            repoBrowser
                .Setup(x => x.IsRepositoryAccessibleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));
            var parser = new ActionParser(repoBrowser.Object);
            var processor = new WorkflowActionProcessor(repoBrowser.Object, new Mock<IActionConfig>().Object, parser, mock.Object);
            try
            {
                await processor.ProcessAsync(tmp.FilePath, false, CancellationToken.None);
            }
            catch (Exception)
            {
                Assert.Fail("should not throw");
            }
        }

        [TestMethod]
        public async Task YmlWithAllReferenceTypesShouldParseSuccessfully()
        {
            using var tmp = FileHelper.ExtractDataFileTemporarily("all-reference-types.yml");

            var mock = new Mock<ILogger<WorkflowActionProcessor>>();
            var repoBrowser = new Mock<IGithubRepositoryBrowser>();
            repoBrowser
                .Setup(x => x.IsRepositoryAccessibleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));
            var parser = new ActionParser(repoBrowser.Object);
            var processor = new WorkflowActionProcessor(repoBrowser.Object, new Mock<IActionConfig>().Object, parser, mock.Object);
            try
            {
                await processor.ProcessAsync(tmp.FilePath, false, CancellationToken.None);
            }
            catch (Exception)
            {
                Assert.Fail("should not throw");
            }
        }

        [DataTestMethod]
        [DataRow("invalid-but-parsable.yml", 2)]
        [DataRow("test.yml", 3)]
        [DataRow("all-reference-types.yml", 2)]
        public async Task CheckingFileShouldNotModifyIt(string file, int updates)
        {
            using var tmp = FileHelper.ExtractDataFileTemporarily(file);

            var mock = new Mock<ILogger<WorkflowActionProcessor>>();
            var repoBrowser = new Mock<IGithubRepositoryBrowser>();
            repoBrowser
                .Setup(x => x.IsRepositoryAccessibleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));
            var parser = new ActionParser(repoBrowser.Object);
            var processor = new WorkflowActionProcessor(repoBrowser.Object, new Mock<IActionConfig>().Object, parser, mock.Object);
            var original = await File.ReadAllTextAsync(tmp.FilePath, CancellationToken.None);
            try
            {
                await processor.ProcessAsync(tmp.FilePath, false, CancellationToken.None);
            }
            catch (Exception)
            {
                Assert.Fail("should not throw");
            }
            var modified = await File.ReadAllTextAsync(tmp.FilePath, CancellationToken.None);
            Assert.AreEqual(original, modified, "because check should not update the file");
            repoBrowser.Verify(x => x.IsRepositoryAccessibleAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Exactly(updates));
        }
    }
}
