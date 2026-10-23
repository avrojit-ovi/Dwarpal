using Dwarpal.Core.Models;

namespace Dwarpal.Core.Security;

public readonly record struct PasswordValidationResult(bool IsValid, string? Error)
{
    public static PasswordValidationResult Ok => new(true, null);
    public static PasswordValidationResult Fail(string error) => new(false, error);
}

/// <summary>
/// Credential policy: PIN 4-8 digit, othoba password (minimum 8 char, letter + number).
/// Setup ar recovery - duijaygay e same rule apply hoy.
/// </summary>
public static class PasswordPolicy
{
    public const int PinMinLength = 4;
    public const int PinMaxLength = 8;
    public const int PasswordMinLength = 8;
    public const int PasswordMaxLength = 64;

    public static PasswordValidationResult Validate(string? secret, CredentialKind kind)
    {
        if (string.IsNullOrEmpty(secret))
        {
            return PasswordValidationResult.Fail(kind == CredentialKind.Pin
                ? "PIN dite hobe."
                : "Password dite hobe.");
        }

        return kind == CredentialKind.Pin ? ValidatePin(secret) : ValidatePassword(secret);
    }

    private static PasswordValidationResult ValidatePin(string pin)
    {
        if (pin.Length < PinMinLength || pin.Length > PinMaxLength)
        {
            return PasswordValidationResult.Fail($"PIN {PinMinLength} theke {PinMaxLength} digit er hote hobe.");
        }

        foreach (var character in pin)
        {
            // Only ASCII digits - Unicode digit gulo allow korle keyboard e confusion hobe.
            if (character is < '0' or > '9')
            {
                return PasswordValidationResult.Fail("PIN e sudhu 0-9 digit allowed.");
            }
        }

        return PasswordValidationResult.Ok;
    }

    private static PasswordValidationResult ValidatePassword(string password)
    {
        if (password.Length < PasswordMinLength || password.Length > PasswordMaxLength)
        {
            return PasswordValidationResult.Fail($"Password {PasswordMinLength} theke {PasswordMaxLength} character er hote hobe.");
        }

        if (password[0] == ' ' || password[^1] == ' ')
        {
            return PasswordValidationResult.Fail("Password er shuru ba sesh e space rakha jabe na.");
        }

        var hasLetter = false;
        var hasDigit = false;

        foreach (var character in password)
        {
            if (char.IsControl(character))
            {
                return PasswordValidationResult.Fail("Password e control character rakha jabe na.");
            }

            if (char.IsLetter(character)) hasLetter = true;
            else if (char.IsDigit(character)) hasDigit = true;
        }

        if (!hasLetter || !hasDigit)
        {
            return PasswordValidationResult.Fail("Password e ekta letter ar ekta number thakte hobe.");
        }

        return PasswordValidationResult.Ok;
    }
}
