using Compile.Shift.Cli.Commands;
using Compile.Shift.Commands;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Compile.Shift.Cli;

internal class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("Domain Migration Definition (DMD) System");

        var host = CreateHostBuilder().Build();
        var mediator = host.Services.GetRequiredService<IMediator>();

        if (args.Length == 0)
        {
            await ExecuteCommandAsync(mediator, new PrintHelpCommand());
            return;
        }

        var command = RequestHelper.GetCommand(args);

        await ExecuteCommandAsync(mediator, command);
    }

    private static IHostBuilder CreateHostBuilder()
    {
        return Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // Register MediatR
                services.AddMediatR(typeof(Program).Assembly);

                // Register Shift services
                services.AddScoped<IShift, Shift>(sp =>
                {
                    var logger = sp.GetRequiredService<ILogger<Shift>>();
                    return new Shift { Logger = logger };
                });

                // Register other services as needed
                services.AddTransient<ModelExporter>();
            })
            .ConfigureLogging(logging =>
            {
                logging.ClearProviders();
                logging.AddSimpleConsole(options =>
                {
                    options.SingleLine = true;
                    options.TimestampFormat = "HH:mm:ss ";
                });
                logging.SetMinimumLevel(LogLevel.Information);
            });
    }

    private static async Task ExecuteCommandAsync<T>(IMediator mediator, T command) where T : IRequest<Unit>
    {
        try
        {
            await mediator.Send(command);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }
}