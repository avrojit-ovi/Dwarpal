using Dwarpal.Core.Models;

namespace Dwarpal.Core.Services;

public interface IActivityLogger
{
    Task LogAsync(
        ActivityEventType eventType,
        ActivityOutcome outcome,
        string? detail = null,
        string? appName = null,
        string? appPath = null,
        string? selfiePath = null,
        CancellationToken cancellationToken = default);
}
