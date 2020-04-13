using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace GithubActionPinner.Core.Tests
{
    public static class FileHelper
    {
        /// <summary>
        /// Given an embedded resource in the Data dir with the specific name will extract its content to a temporary file and return its path.
        /// Dispose the returned object to automatically clean up the file.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="action">The name to insert into the file (assumes the file has a hardcoded placeholder __ACTION__)</param>
        public static DisposableFileWrapper<string> ExtractAndTransformDataFileTemporarily(string name, string action)
            => ExtractAndTransformDataFileTemporarily(name, "__ACTION__", action);

        /// <summary>
        /// Given an embedded resource in the Data dir with the specific name will extract its content to a temporary file and return its path.
        /// Dispose the returned object to automatically clean up the file.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="toReplace">The string in the file to replace with the action</param>
        /// <param name="action">The name to insert into the file in place of <see cref="toReplace"/></param>
        public static DisposableFileWrapper<string> ExtractAndTransformDataFileTemporarily(string name, string toReplace, string action)
        {
            var tmp = Path.GetTempFileName();
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(typeof(WorkflowActionProcessorTests).Namespace + ".Data." + name))
            using (var file = File.OpenWrite(tmp))
                stream.CopyTo(file);

            if (!string.IsNullOrEmpty(toReplace))
                File.WriteAllText(tmp, File.ReadAllText(tmp).Replace(toReplace, action));

            return new DisposableFileWrapper<string>(() =>
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

        /// <summary>
        /// Given an embedded resource in the Data dir with the specific name will extract its content to a temporary file and return its path.
        /// Dispose the returned object to automatically clean up the file.
        /// </summary>
        public static DisposableFileWrapper<string> ExtractDataFileTemporarily(string name)
            => ExtractAndTransformDataFileTemporarily(name, null, "");

        public class DisposableFileWrapper<T> : IDisposable
        {
            private readonly Action _action;

            public T FilePath { get; }

            public DisposableFileWrapper(Action action, T data)
            {
                _action = action;
                FilePath = data;
            }

            public void Dispose()
                => _action();
        }
    }
}
