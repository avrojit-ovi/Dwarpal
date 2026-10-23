namespace Dwarpal.Core.Models;

public enum ActivityEventType
{
    Setup,
    Unlock,
    Lock,
    Recovery,
    Settings,
    Tamper,
    Error,
}

public enum ActivityOutcome
{
    Success,
    Failure,
    Info,
}

public sealed record ActivityEntry(
    long Id,
    DateTimeOffset TimestampUtc,
    ActivityEventType EventType,
    string? AppName,
    string? AppPath,
    ActivityOutcome Outcome,
    string? Detail,
    string? SelfiePath);

public sealed record ActivityEntryDraft(
    DateTimeOffset TimestampUtc,
    ActivityEventType EventType,
    ActivityOutcome Outcome,
    string? AppName = null,
    string? AppPath = null,
    string? Detail = null,
    string? SelfiePath = null);
