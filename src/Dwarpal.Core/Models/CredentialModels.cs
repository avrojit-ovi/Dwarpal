namespace Dwarpal.Core.Models;

public enum CredentialKind
{
    Pin,
    Password,
}

/// <summary>
/// Ek e user credential (single row). Secret ar recovery key dutoi one-way hash, plaintext kokhono save hoy na.
/// </summary>
public sealed record CredentialRecord(
    CredentialKind Kind,
    string SecretHash,
    string RecoveryHash,
    DateTimeOffset CreatedUtc,
    DateTimeOffset UpdatedUtc);
