using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace GithubActionPinner
{
    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            if (args.Length != 2 || !File.Exists(args[1]))
            {
                Console.WriteLine("Usage:");
                Console.WriteLine("[yml-file-path]");
                Console.WriteLine("Will scan the yml file for Github actions that can be pinned to their respective SHA or updated to a newer version.");
                return -1;
            }
            var mode = args[0].ToLowerInvariant() switch
            {
                "update" => Mode.Update,
                "check" => Mode.Check,
                _ => Mode.Unknown
            };
            var file = args[1];

            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) => cts.Cancel();
            try
            {
                return await RunAsync(file, mode, cts.Token);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return -2;
            }
        }

        private static async Task<int> RunAsync(string file, Mode mode, CancellationToken cancellationToken)
        {
            // could parse file for validity but string manipulation is much easier #famousLastWords
            var lines = await File.ReadAllLinesAsync(file, cancellationToken);
            for (int i = 0; i < lines.Length; i++)
            {
                if (!HasActionReference(lines[i]))
                    continue;

                var info = await ParseActionAsync(new Line
                {
                    LineNumber = i + 1,
                    Text = lines[i]
                }, cancellationToken);
                if (info.IsDocker || !info.IsPublic)
                    continue;

                if (info.Pinned.HasValue)
                {
                    var currentSha = info.Sha;
                    if (info.Pinned.Value.VersionReferenceType == ActionVersionReferenceType.Tag)
                    {
                        // update can be:
                        //  - same version but different SHA
                        //  - new minor version
                        var currentVersion = info.Pinned.Value.Version;
                    }
                }
                else if (info.VersionReferenceType != ActionVersionReferenceType.SHA)
                {
                    // never pinned before and not a SHA -> pin @current
                }
            }
            return 0;
        }

        private static Task<dynamic> ParseActionAsync(Line line, CancellationToken cancellationToken)
        {
            // expected format: "  - uses: action/foo@<version> [# comment]"
            var text = line.Text.Trim();
            if (!text.StartsWith("- uses:"))
                throw new NotSupportedException($"Action references must start with '- uses:', line ({line.LineNumber}) {line.Text} is invalid");
            var remainder = text.Substring("- uses:".Length).TrimStart();
            string actionRef = remainder;
            string? comment;
            var idx = remainder.IndexOf('#');
            if (idx > -1)
            {
                actionRef = remainder.Substring(idx);
                comment = remainder.Substring(idx + 1);
            }
            return null;
        }

        private static bool HasActionReference(string line)
            => line.Trim().Contains("- uses:");

        private struct Line
        {
            public int LineNumber { get; set; }

            public string Text { get; set; }
        }

        private enum Mode
        {
            Unknown = 0,
            Update,
            Check
        }
    }
}
