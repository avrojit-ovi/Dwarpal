using Dwarpal.Core.Data;
using Dwarpal.Core.Models;
using Serilog;

namespace Dwarpal.Core.Services;

/// <summary>
/// Activity log ek jaygay theke dui dike jay: SQLite (UI table/CSV) ar Serilog file (diagnostics).
/// Event write fail korle app crash hobe na - log e warning jabe.
/// </summary>
public sealed class ActivityLogger : IActivityLogger
{
    private readonly ActivityRepository _repository;

    public ActivityLogger(ActivityRepository repository) => _repository = repository;

    public async Task LogAsync(
        ActivityEventType eventType,
        ActivityOutcome outcome,
        string? detail = null,
        string? appName = null,
        string? appPath = null,
        string? selfiePath = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var draft = new ActivityEntryDraft(
                DateTimeOffset.UtcNow, eventType, outcome, appName, appPath, detail, selfiePath);

            await _repository.AddAsync(draft, cancellationToken);
        }
        catch (Exception ex)
        {
            // Audit write fail hole core feature fail korbe na, kintu chup thakbe na.
            Log.Warning(ex, "Failed to persist activity entry {EventType}/{Outcome}", eventType, outcome);
        }

        switch (outcome)
        {
            case ActivityOutcome.Failure:
                Log.Warning("{EventType}/{Outcome}: {Detail} (app: {AppName})", eventType, outcome, detail, appName);
                break;
            default:
                Log.Information("{EventType}/{Outcome}: {Detail} (app: {AppName})", eventType, outcome, detail, appName);
                break;
        }
    }
}
