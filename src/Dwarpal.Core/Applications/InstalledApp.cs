namespace Dwarpal.Core.Applications;

public enum InstalledAppSource
{
    Registry,
    StartMenu,
    Manual,
}

public sealed record InstalledApp(
    string DisplayName,
    string ExecutablePath,
    string? IconPath,
    InstalledAppSource Source);
