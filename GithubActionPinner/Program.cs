﻿using GithubActionPinner.Core;
using GithubActionPinner.Core.Config;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace GithubActionPinner
{
    public class Program
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

        private static IServiceCollection SetupDI(string? githubApiToken)
        {
            return new ServiceCollection()
                   .AddLogging(builder => builder.AddProvider(new ConsoleLoggerProvider()))
                   .AddTransient<IActionConfig, ActionConfig>()
                   .AddTransient<IActionParser, ActionParser>()
                   .AddTransient<WorkflowActionProcessor>()
                   .AddSingleton(_ => (IGithubRepositoryBrowser)new GithubRepositoryBrowser(githubApiToken));
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
                var logger = sp.GetRequiredService<ILogger<Program>>();
                var update = mode == Mode.Update;
                var config = sp.GetRequiredService<IActionConfig>();
                const string fileName = "GithubActionPinner.trusted";
                string configFile = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? "", fileName);
                // config file is optional
                if (File.Exists(configFile))
                {
                    config.Load(configFile);
                }
                foreach (var file in filesToProcess)
                {
                    try
                    {
                        await processor.ProcessAsync(file, update, cancellationToken);
                    }
                    catch (GithubApiRatelimitExceededException ex)
                    {
                        logger.LogError(ex.Message);
                        if (!ex.WasAuthenticated)
                        {
                            logger.LogError("For unauthenticated requests the ratelimit is rather low, consider authenticating with a personal access token: https://help.github.com/en/github/authenticating-to-github/creating-a-personal-access-token-for-the-command-line to increase your limit.");
                            logger.LogError("Pass the token either via `--token` argument or set it as the `GITHUB_TOKEN` environment variable.");
                        }
                        // TODO: if cache is introduced all other files could be processed and may even be up to date
                        break;
                    }
                }
                processor.Summarize();
            }
        }

        private static (string fileOrFolder, Mode mode, string? githubApiToken) ParseArguments(IConfiguration configuration)
        {
            var update = configuration["update"];
            var check = configuration["check"];
            if ((update == null && check == null) ||
                (update != null && check != null))
                throw new ArgumentException("Either --update or --check must be set");

            var mode = update != null ? Mode.Update : Mode.Check;
            var fileOrFolder = update ?? check ?? throw new InvalidProgramException("compiler not smart");

            var githubApiToken = configuration["token"] ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            return (fileOrFolder, mode, githubApiToken);
        }
    }
}
