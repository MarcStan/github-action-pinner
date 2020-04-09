using GithubActionPinner.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
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
                await RunAsync(file, mode, cts.Token);
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return -2;
            }
        }

        private static async Task RunAsync(string file, Mode mode, CancellationToken cancellationToken)
        {
            var services = new ServiceCollection()
                .AddLogging(builder => builder.AddConsole())
                .AddTransient<WorkflowActionProcessor>()
                .AddTransient<IGithubRepositoryBrowser, GithubRepositoryBrowser>();

            using (var sp = services.BuildServiceProvider())
            {
                var processor = sp.GetRequiredService<WorkflowActionProcessor>();
                await processor.ProcessAsync(file, mode == Mode.Update, cancellationToken);
            }
        }
    }
}
