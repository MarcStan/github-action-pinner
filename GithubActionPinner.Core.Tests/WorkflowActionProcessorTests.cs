using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.IO;
using System.Reflection;
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
            using var tmp = ExtractAndTransformDataFileTemporarily("one-action-transformed.yml", currentVersion == null ? actionName : $"{actionName}@{currentVersion}");

            var repoBrowser = new Mock<IGithubRepositoryBrowser>();
            repoBrowser
                .Setup(x => x.IsPublicAsync(owner, repo, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));
            (string, string)? response = (latestVersion, latestSha);
            repoBrowser
                .Setup(x => x.GetLatestSemVerCompliantAsync(owner, repo, currentVersion, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(response));
            repoBrowser
                .Setup(x => x.GetShaForLatestCommitAsync(owner, repo, latestVersion, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(latestSha));

            var processor = new WorkflowActionProcessor(repoBrowser.Object, new Mock<ILogger<WorkflowActionProcessor>>().Object);
            try
            {
                await processor.ProcessAsync(tmp.Data, true, CancellationToken.None);
                var file = File.ReadAllText(tmp.Data);
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
                Assert.IsTrue(file.Contains($"{actionName}@{latestSha} # @{latestVersion}"));
            }
            catch
            {
                Assert.Fail("should not throw");
            }
            repoBrowser.Verify(x => x.IsPublicAsync(owner, repo, It.IsAny<CancellationToken>()), Times.Once);
            // can be tag or branch
            if (!string.IsNullOrEmpty(currentVersion) && VersionHelper.TryParse(currentVersion, out _))
                repoBrowser.Verify(x => x.GetLatestSemVerCompliantAsync(owner, repo, currentVersion, It.IsAny<CancellationToken>()), Times.Once);
            else
                repoBrowser.Verify(x => x.GetShaForLatestCommitAsync(owner, repo, latestVersion, It.IsAny<CancellationToken>()), Times.Once);
        }

        [DataTestMethod]
        [DataRow("actions", "checkout", "de4cd7198fed4a740bdc2073abeb76e496c7c6fe", "v2", "v2", "de4cd7198fed4a740bdc2073abeb76e496c7c6fe")]
        public async Task PinnedActionShouldBeUpdatedIfPossible(string owner, string repo, string currentSha, string currentVersion, string latestVersion, string latestSha)
        {
            var actionName = $"{owner}/{repo}";
            using var tmp = ExtractAndTransformDataFileTemporarily("one-action-transformed.yml", $"{actionName}@{currentSha} # @{currentVersion}");

            var repoBrowser = new Mock<IGithubRepositoryBrowser>();
            repoBrowser
                .Setup(x => x.IsPublicAsync(owner, repo, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));
            (string, string)? response = (latestVersion, latestSha);
            repoBrowser
                .Setup(x => x.GetLatestSemVerCompliantAsync(owner, repo, currentVersion, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(response));
            repoBrowser
                .Setup(x => x.GetShaForLatestCommitAsync(owner, repo, latestVersion, It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(latestSha));

            var processor = new WorkflowActionProcessor(repoBrowser.Object, new Mock<ILogger<WorkflowActionProcessor>>().Object);
            try
            {
                await processor.ProcessAsync(tmp.Data, true, CancellationToken.None);
                var file = File.ReadAllText(tmp.Data);
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
                Assert.IsTrue(file.Contains($"{actionName}@{latestSha} # @{latestVersion}"));
            }
            catch
            {
                Assert.Fail("should not throw");
            }
            repoBrowser.Verify(x => x.IsPublicAsync(owner, repo, It.IsAny<CancellationToken>()), Times.Once);
            // can be tag or branch
            if (!string.IsNullOrEmpty(currentVersion) && VersionHelper.TryParse(currentVersion, out _))
                repoBrowser.Verify(x => x.GetLatestSemVerCompliantAsync(owner, repo, currentVersion, It.IsAny<CancellationToken>()), Times.Once);
            else
                repoBrowser.Verify(x => x.GetShaForLatestCommitAsync(owner, repo, latestVersion, It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task ValidYmlFileShouldParseSuccessfully()
        {
            using var tmp = ExtractDataFileTemporarily("test.yml");

            var repo = new Mock<IGithubRepositoryBrowser>();
            var processor = new WorkflowActionProcessor(repo.Object, new Mock<ILogger<WorkflowActionProcessor>>().Object);
            try
            {
                await processor.ProcessAsync(tmp.Data, false, CancellationToken.None);
            }
            catch (Exception)
            {
                Assert.Fail("should not throw");
            }
        }

        [TestMethod]
        public async Task InvalidYmlFileShouldIgnoreInvalidLinesButParseSuccessfully()
        {
            using var tmp = ExtractDataFileTemporarily("invalid-but-parsable.yml");

            var mock = new Mock<ILogger<WorkflowActionProcessor>>();
            var repo = new Mock<IGithubRepositoryBrowser>();
            var processor = new WorkflowActionProcessor(repo.Object, mock.Object);
            try
            {
                await processor.ProcessAsync(tmp.Data, false, CancellationToken.None);
            }
            catch (Exception)
            {
                Assert.Fail("should not throw");
            }
        }

        [TestMethod]
        public async Task YmlWithAllReferenceTypesShouldParseSuccessfully()
        {
            using var tmp = ExtractDataFileTemporarily("all-reference-types.yml");

            var mock = new Mock<ILogger<WorkflowActionProcessor>>();
            var repo = new Mock<IGithubRepositoryBrowser>();
            var processor = new WorkflowActionProcessor(repo.Object, mock.Object);
            try
            {
                await processor.ProcessAsync(tmp.Data, false, CancellationToken.None);
            }
            catch (Exception)
            {
                Assert.Fail("should not throw");
            }
        }

        private DisposableWrapper<string> ExtractAndTransformDataFileTemporarily(string name, string action)
            => ExtractAndTransformDataFileTemporarily(name, "__ACTION__", action);

        private DisposableWrapper<string> ExtractAndTransformDataFileTemporarily(string name, string toReplace, string action)
        {
            var tmp = Path.GetTempFileName();
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(typeof(WorkflowActionProcessorTests).Namespace + ".Data." + name))
            using (var file = File.OpenWrite(tmp))
                stream.CopyTo(file);

            if (!string.IsNullOrEmpty(toReplace))
                File.WriteAllText(tmp, File.ReadAllText(tmp).Replace(toReplace, action));

            return new DisposableWrapper<string>(() =>
            {
                try
                {
                    File.Delete(tmp);
                }
                catch (IOException)
                {
                }
            }, tmp);
        }

        private DisposableWrapper<string> ExtractDataFileTemporarily(string name)
            => ExtractAndTransformDataFileTemporarily(name, null, "");

        private class DisposableWrapper<T> : IDisposable
        {
            private readonly Action _action;

            public T Data { get; }

            public DisposableWrapper(Action action, T data)
            {
                _action = action;
                Data = data;
            }

            public void Dispose()
                => _action();
        }
    }
}
