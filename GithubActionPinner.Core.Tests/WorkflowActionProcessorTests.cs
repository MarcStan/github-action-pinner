using Microsoft.Extensions.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace GithubActionPinner.Core.Tests
{
    [TestClass]
    public class WorkflowActionProcessorTests
    {
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

        private DisposableWrapper<string> ExtractDataFileTemporarily(string name)
        {
            var tmp = Path.GetTempFileName();
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(typeof(WorkflowActionProcessorTests).Namespace + ".Data." + name))
            using (var file = File.OpenWrite(tmp))
                stream.CopyTo(file);

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
