using Serilog;
using Serilog.Events;

namespace Dwarpal.Core.Logging;

/// <summary>
/// App ar Service duijon e same rolling log file e likhe (shared sink).
/// </summary>
public static class LoggingSetup
{
    private const string Template =
        "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] ({SourceContext}) {Message:lj}{NewLine}{Exception}";

    public static void Initialize(AppPaths paths)
    {
        Directory.CreateDirectory(paths.LogDirectory);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
            .Enrich.FromLogContext()
            .WriteTo.File(
                Path.Combine(paths.LogDirectory, "dwarpal-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 14,
                shared: true,
                outputTemplate: Template)
            .CreateLogger();
    }
}
