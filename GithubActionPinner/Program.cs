using GithubActionPinner.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Globalization;
using System.IO;
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

        private static async Task RunAsync(IConfiguration configuration, CancellationToken cancellationToken)
        {
            var update = configuration["update"];
            var check = configuration["check"];
            if ((update == null && check == null) ||
                (update != null && check != null))
                throw new ArgumentException("Either --update or --check must be set");

            var mode = update != null ? Mode.Update : Mode.Check;
            var file = update ?? check ?? throw new InvalidProgramException("compiler not smart");

            if (!File.Exists(file))
                throw new ArgumentException($"File '{file}' not found.");

            var token = configuration["token"];

            var services = new ServiceCollection()
                .AddLogging(builder => builder.AddProvider(new ConsoleLoggerProvider()))
                .AddTransient<WorkflowActionProcessor>()
                .AddSingleton(_ => (IGithubRepositoryBrowser)new GithubRepositoryBrowser(token));

            using (var sp = services.BuildServiceProvider())
            {
                var processor = sp.GetRequiredService<WorkflowActionProcessor>();
                await processor.ProcessAsync(file, mode == Mode.Update, cancellationToken);
            }
        }
    }
}
