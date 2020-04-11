using GithubActionPinner.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GithubActionPinner
{
    public static class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) => cts.Cancel();

            var configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build();

            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            try
            {
                await RunAsync(configuration, cts.Token);
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return -2;
            }
        }

        private static IServiceCollection SetupDI(string token)
        {
            return new ServiceCollection()
                   .AddLogging(builder => builder.AddProvider(new ConsoleLoggerProvider()))
                   .AddTransient<IActionParser, ActionParser>()
                   .AddTransient<WorkflowActionProcessor>()
                   .AddSingleton(_ => (IGithubRepositoryBrowser)new GithubRepositoryBrowser(token));
        }

        private static async Task RunAsync(IConfiguration configuration, CancellationToken cancellationToken)
        {
            var (fileOrFolder, mode, token) = ParseArguments(configuration);

            string[] filesToProcess;
            if (Directory.Exists(fileOrFolder))
            {
                // https://help.github.com/en/actions/reference/workflow-syntax-for-github-actions#about-yaml-syntax-for-workflows
                filesToProcess = Directory.EnumerateFiles(fileOrFolder, "*.y*", SearchOption.AllDirectories)
                    .Where(f =>
                    {
                        if (!f.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) &&
                            !f.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
                            return false;
                        return Path.GetDirectoryName(f)?.Replace("\\", "/")?.EndsWith(".github/workflows", StringComparison.OrdinalIgnoreCase) ?? false;
                    })
                    .ToArray();

                if (!filesToProcess.Any())
                    throw new ArgumentException($"No matching action files found in directory '{fileOrFolder}'.");
            }
            else
            {
                if (!File.Exists(fileOrFolder))
                    throw new ArgumentException($"Action file '{fileOrFolder}' not found.");

                filesToProcess = new[] { fileOrFolder };
            }

            var services = SetupDI(token);

            using (var sp = services.BuildServiceProvider())
            {
                var processor = sp.GetRequiredService<WorkflowActionProcessor>();
                var update = mode == Mode.Update;
                foreach (var file in filesToProcess)
                {
                    await processor.ProcessAsync(file, update, cancellationToken);
                }
            }
        }

        private static (string fileOrFolder, Mode mode, string token) ParseArguments(IConfiguration configuration)
        {
            var update = configuration["update"];
            var check = configuration["check"];
            if ((update == null && check == null) ||
                (update != null && check != null))
                throw new ArgumentException("Either --update or --check must be set");

            var mode = update != null ? Mode.Update : Mode.Check;
            var fileOrFolder = update ?? check ?? throw new InvalidProgramException("compiler not smart");

            var token = configuration["token"];
            return (fileOrFolder, mode, token);
        }
    }
}
