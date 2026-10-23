using Dwarpal.Core;
using Dwarpal.Core.Data;
using Serilog;

namespace Dwarpal.Service;

/// <summary>
/// Privileged heartbeat/diagnostics host. Interactive tamper watchdog Task Scheduler e chole,
/// karon session-0 service theke user er desktop e Dwarpal UI dekhano jay na.
/// </summary>
public sealed class Worker : BackgroundService
{
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(30);

    private readonly DwarpalDatabase _database;
    private readonly AppPaths _paths;

    public Worker(DwarpalDatabase database, AppPaths paths)
    {
        _database = database;
        _paths = paths;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _database.InitializeAsync(stoppingToken);

        Log.Information("Dwarpal service online. Data directory: {RootDirectory}", _paths.RootDirectory);

        using var timer = new PeriodicTimer(HeartbeatInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                Log.Debug("Service heartbeat");
            }
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }

        Log.Information("Dwarpal service stopped.");
    }
}
