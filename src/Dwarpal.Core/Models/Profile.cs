namespace Dwarpal.Core.Models;

/// <summary>
/// Named subset of protected apps (e.g. "Work", "Personal"). Ek somoy e ekta profile active
/// thake; active profile null (Id 0) mane sob enabled app protect hoy.
/// </summary>
public sealed record Profile(
    int Id,
    string Name,
    DateTimeOffset CreatedUtc,
    int AppCount = 0)
{
    public const int MaxNameLength = 40;
    public const int AllAppsProfileId = 0;

    /// <summary>Null return mane name valid; nahole user-facing error.</summary>
    public static string? ValidateName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return "Profile er naam dao.";
        }

        return name.Trim().Length > MaxNameLength
            ? $"Naam {MaxNameLength} character er beshi hote parbe na."
            : null;
    }
}
