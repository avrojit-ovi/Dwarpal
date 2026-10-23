namespace Dwarpal.Core.Services;

/// <summary>
/// Failed unlock er por intruder photo capture. Implementation camera na pele null return kore -
/// unlock flow kokhono camera er upor depend korbe na.
/// </summary>
public interface IIntruderSelfieService
{
    Task<string?> CaptureAsync(CancellationToken cancellationToken = default);
}
