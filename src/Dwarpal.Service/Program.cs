using Dwarpal.Core;
using Dwarpal.Core.Data;
using Dwarpal.Core.Logging;
using Dwarpal.Service;
using Serilog;

var paths = new AppPaths();
paths.EnsureCreated();
LoggingSetup.Initialize(paths);

Log.Information("Dwarpal.Service booting. Interactive: {Interactive}", Environment.UserInteractive);

try
{
    var builder = Host.CreateApplicationBuilder(args);

    builder.Services.AddWindowsService(options => options.ServiceName = "DwarpalService");
    builder.Services.AddSingleton(paths);
    builder.Services.AddSingleton(new DwarpalDatabase(paths.DatabasePath));
    builder.Services.AddHostedService<Worker>();

    var host = builder.Build();
    await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Dwarpal.Service terminated unexpectedly");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
