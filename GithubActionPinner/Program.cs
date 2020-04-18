using GithubActionPinner.Core;
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
            if (args.Length == 0 || IsHelp(args[0]))
            {
                Console.WriteLine("Usage:");
                Console.WriteLine("<exe> --update file|folder [--token GH_TOKEN]");
                Console.WriteLine("");
                Console.WriteLine("  Checks the file/folder for actions to update.");
                Console.WriteLine("  Folder will be searched recursively for *.yml/*.yaml files in all '.github/workflows' directories.");
                Console.WriteLine("  Each action file is parsed for actions and github api is called to check for available updates");
                Console.WriteLine("");
                Console.WriteLine("  Token is optional but encouraged due to githubs ratelimit (token requires repo permissions)");
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine("<exe> --check file|folder [--token GH_TOKEN]");
                Console.WriteLine("");
                Console.WriteLine("  Same as update but no files will actually be modified. Results will only be printed to console.");
                Console.WriteLine();
                Console.WriteLine();
                Console.WriteLine($"{ExeName()}.trusted can be used to trust orgs/actions or specific commits. Place the file next to the exe and add one entry per line to trust them.");
                return 0;
            }
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) => cts.Cancel();

            var configuration = new ConfigurationBuilder()
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build();

            Thread.CurrentThread.CurrentCulture = CultureInfo.InvariantCulture;
            try
            {
                await RunAsync(configuration, cts.Token).ConfigureAwait(false);
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return -2;
            }
        }

        private static bool IsHelp(string arg)
            => new[] { "-h", "--help", "/help" }.Contains(arg.ToLowerInvariant());

        private static IServiceCollection SetupDI(string? githubApiToken)
        {
            return new ServiceCollection()
                   .AddLogging(builder => builder.AddProvider(new ConsoleLoggerProvider()))
                   .AddSingleton<IActionConfig, ActionConfig>()
                   .AddTransient<IActionParser, ActionParser>()
                   .AddSingleton(new CachedGithubApi(githubApiToken))
                   .AddTransient<WorkflowActionProcessor>()
                   .AddTransient<IGithubRepositoryBrowser, GithubRepositoryBrowser>();
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
                        {
                            return false;
                        }

                        return Path.GetDirectoryName(f)?.Replace("\\", "/")?.EndsWith(".github/workflows", StringComparison.OrdinalIgnoreCase) ?? false;
                    })
                    .ToArray();

                if (filesToProcess.Length == 0)
                    throw new ArgumentException($"No matching action files found in directory '{fileOrFolder}'.");
            }
            else
            {
                if (!File.Exists(fileOrFolder))
                    throw new ArgumentException($"Action file '{fileOrFolder}' not found.");

                filesToProcess = new[] { fileOrFolder };
            }

            var services = SetupDI(token);

            using var sp = services.BuildServiceProvider();
            var processor = sp.GetRequiredService<WorkflowActionProcessor>();
            var logger = sp.GetRequiredService<ILogger<Program>>();
            var update = mode == Mode.Update;
            var config = sp.GetRequiredService<IActionConfig>();
            var fileName = $"{ExeName()}.trusted";
            string configFile = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".", fileName);
            // config file is optional
            if (File.Exists(configFile))
            {
                config.Load(configFile);
            }
            GithubApiRatelimitExceededException? rateLimitError = null;
            foreach (var file in filesToProcess)
            {
                try
                {
                    await processor.ProcessAsync(file, update, cancellationToken).ConfigureAwait(false);
                }
                catch (GithubApiRatelimitExceededException ex)
                {
                    // with cache there is a small chance that everything else is already cached, so keep going
                    rateLimitError = ex;
                }
            }
            if (rateLimitError != null)
            {
                logger.LogError(rateLimitError.Message);
                if (!rateLimitError.WasAuthenticated)
                {
                    logger.LogError("For unauthenticated requests the ratelimit is rather low, consider authenticating with a personal access token to increase your limit (https://help.github.com/en/github/authenticating-to-github/creating-a-personal-access-token-for-the-command-line).");
                    logger.LogError("Pass the token either via `--token` argument or set it as the `GITHUB_TOKEN` environment variable.");
                }
            }
            processor.Summarize();
        }

        private static string ExeName()
            => Path.GetFileNameWithoutExtension(typeof(Program).Assembly.Location);

        private static (string fileOrFolder, Mode mode, string? githubApiToken) ParseArguments(IConfiguration configuration)
        {
            var update = configuration["update"];
            var check = configuration["check"];
            if ((update == null && check == null) ||
                (update != null && check != null))
            {
                throw new ArgumentException("Either --update or --check must be set");
            }

            var mode = update != null ? Mode.Update : Mode.Check;
            var fileOrFolder = update ?? check ?? throw new InvalidProgramException("compiler not smart");

            var githubApiToken = configuration["token"] ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN");
            return (fileOrFolder, mode, githubApiToken);
        }
    }
}
