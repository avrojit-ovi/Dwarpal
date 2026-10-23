using Dwarpal.Core.Data;
using Dwarpal.Core.Models;
using Dwarpal.Core.Services;

namespace Dwarpal.Core.Security;

public sealed record SetupResult(bool Success, string? Error, string? RecoveryKey)
{
    public static SetupResult Ok(string recoveryKey) => new(true, null, recoveryKey);
    public static SetupResult Fail(string error) => new(false, error, null);
}

public sealed record VerificationResult(bool Success, string? Error, int LockoutSeconds = 0);

public sealed record RecoveryResetResult(bool Success, string? Error, string? NewRecoveryKey)
{
    public static RecoveryResetResult Ok(string newRecoveryKey) => new(true, null, newRecoveryKey);
    public static RecoveryResetResult Fail(string error) => new(false, error, null);
}

public sealed record ChangeCredentialResult(bool Success, string? Error)
{
    public static ChangeCredentialResult Ok() => new(true, null);
    public static ChangeCredentialResult Fail(string error) => new(false, error);
}

/// <summary>
/// Credential lifecycle: first-time setup, verify, change ar recovery-key reset.
/// Plaintext secret kokhono store hoy na - shudhu Argon2id hash.
/// </summary>
public sealed class CredentialService
{
    private readonly CredentialRepository _repository;
    private readonly ISecretHasher _hasher;
    private readonly IActivityLogger _activity;
    private readonly LockoutService? _lockout;
    private readonly IIntruderSelfieService? _selfies;

    public CredentialService(
        CredentialRepository repository,
        ISecretHasher hasher,
        IActivityLogger activity,
        LockoutService? lockout = null,
        IIntruderSelfieService? selfies = null)
    {
        _repository = repository;
        _hasher = hasher;
        _activity = activity;
        _lockout = lockout;
        _selfies = selfies;
    }

    public Task<bool> IsConfiguredAsync(CancellationToken cancellationToken = default)
        => _repository.ExistsAsync(cancellationToken);

    public async Task<CredentialKind?> GetKindAsync(CancellationToken cancellationToken = default)
        => (await _repository.LoadAsync(cancellationToken))?.Kind;

    public async Task<SetupResult> SetupAsync(CredentialKind kind, string secret, CancellationToken cancellationToken = default)
    {
        var validation = PasswordPolicy.Validate(secret, kind);
        if (!validation.IsValid)
        {
            return SetupResult.Fail(validation.Error!);
        }

        if (await _repository.ExistsAsync(cancellationToken))
        {
            return SetupResult.Fail("Credential already configured. Use change credential instead.");
        }

        var recoveryKey = RecoveryKey.Generate();
        var canonicalRecoveryKey = NormalizeRecoveryKeyOrThrow(recoveryKey);
        var now = DateTimeOffset.UtcNow;

        var record = new CredentialRecord(
            kind,
            _hasher.Hash(secret),
            _hasher.Hash(canonicalRecoveryKey),
            now,
            now);

        await _repository.SaveAsync(record, cancellationToken);

        await _activity.LogAsync(
            ActivityEventType.Setup,
            ActivityOutcome.Success,
            $"Initial {kind} credential configured.",
            cancellationToken: cancellationToken);

        return SetupResult.Ok(recoveryKey);
    }

    /// <summary>
    /// Unlock screen theke call hoy. Success/failure duitoi activity log e jay.
    /// </summary>
    public async Task<VerificationResult> VerifyAsync(
        string secret,
        string context,
        CancellationToken cancellationToken = default)
    {
        if (_lockout is not null)
        {
            var current = await _lockout.GetStatusAsync(cancellationToken);
            if (current.IsLockedOut)
            {
                await _activity.LogAsync(
                    ActivityEventType.Unlock,
                    ActivityOutcome.Failure,
                    $"{context}: attempt rejected, lockout {current.RemainingSeconds}s remaining.",
                    cancellationToken: cancellationToken);

                return new VerificationResult(
                    false,
                    $"Too many attempts. Try again in {current.RemainingSeconds}s.",
                    current.RemainingSeconds);
            }
        }

        var record = await _repository.LoadAsync(cancellationToken);
        if (record is null)
        {
            return new VerificationResult(false, "Kono credential setup kora nei.");
        }

        var isValid = _hasher.Verify(secret, record.SecretHash);

        if (!isValid)
        {
            string error = "Bhul PIN ba password.";
            var lockoutSeconds = 0;
            var detail = $"{context}: wrong credential.";
            string? selfiePath = null;

            if (_lockout is not null)
            {
                var status = await _lockout.RegisterFailureAsync(cancellationToken);
                lockoutSeconds = status.RemainingSeconds;

                if (status.IsLockedOut)
                {
                    error = $"Too many wrong attempts. Locked for {status.RemainingSeconds}s.";
                    detail += $" Lockout triggered for {status.RemainingSeconds}s.";
                }
                else
                {
                    var left = Math.Max(0, status.MaxAttempts - status.FailedAttempts);
                    error = $"Bhul PIN ba password. {left} attempt(s) baki.";
                    detail += $" {left} attempt(s) left.";
                }

                // Teen fail er por ar lockout trigger e ekbar photo - bar bar camera chalu hobe na.
                if (_selfies is not null && (status.IsLockedOut || status.FailedAttempts == 3))
                {
                    try
                    {
                        selfiePath = await _selfies.CaptureAsync(cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Warning(ex, "Intruder selfie capture failed");
                    }
                }
            }

            await _activity.LogAsync(
                ActivityEventType.Unlock,
                ActivityOutcome.Failure,
                detail,
                selfiePath: selfiePath,
                cancellationToken: cancellationToken);

            return new VerificationResult(false, error, lockoutSeconds);
        }

        if (_lockout is not null)
        {
            await _lockout.ResetAsync(cancellationToken);
        }

        await _activity.LogAsync(
            ActivityEventType.Unlock,
            ActivityOutcome.Success,
            $"{context}: unlock successful.",
            cancellationToken: cancellationToken);

        return new VerificationResult(true, null);
    }

    public async Task<ChangeCredentialResult> ChangeCredentialAsync(
        string currentSecret,
        CredentialKind newKind,
        string newSecret,
        CancellationToken cancellationToken = default)
    {
        var record = await _repository.LoadAsync(cancellationToken);
        if (record is null)
        {
            return ChangeCredentialResult.Fail("Kono credential setup kora nei.");
        }

        var currentIsValid = _hasher.Verify(currentSecret, record.SecretHash);
        if (!currentIsValid)
        {
            await _activity.LogAsync(
                ActivityEventType.Settings, ActivityOutcome.Failure,
                "Credential change rejected: current credential incorrect.",
                cancellationToken: cancellationToken);

            return ChangeCredentialResult.Fail("Current PIN/password bhul.");
        }

        var validation = PasswordPolicy.Validate(newSecret, newKind);
        if (!validation.IsValid)
        {
            return ChangeCredentialResult.Fail(validation.Error!);
        }

        var updated = record with
        {
            Kind = newKind,
            SecretHash = _hasher.Hash(newSecret),
            UpdatedUtc = DateTimeOffset.UtcNow,
        };

        await _repository.SaveAsync(updated, cancellationToken);

        if (_lockout is not null)
        {
            await _lockout.ResetAsync(cancellationToken);
        }

        await _activity.LogAsync(
            ActivityEventType.Settings, ActivityOutcome.Success,
            $"Credential changed to {newKind}.",
            cancellationToken: cancellationToken);

        return ChangeCredentialResult.Ok();
    }

    /// <summary>
    /// Recovery key diye naya credential set. Sob recovery attempt (fail o) log hoy,
    /// ar notun recovery key generate hoy - purano ta ar kaj korbe na.
    /// </summary>
    public async Task<RecoveryResetResult> ResetWithRecoveryKeyAsync(
        string recoveryKeyInput,
        CredentialKind newKind,
        string newSecret,
        CancellationToken cancellationToken = default)
    {
        var record = await _repository.LoadAsync(cancellationToken);
        if (record is null)
        {
            return RecoveryResetResult.Fail("Kono credential setup kora nei.");
        }

        if (!RecoveryKey.TryNormalize(recoveryKeyInput, out var normalizedKey))
        {
            await _activity.LogAsync(
                ActivityEventType.Recovery, ActivityOutcome.Failure,
                "Recovery rejected: malformed recovery key.",
                cancellationToken: cancellationToken);

            return RecoveryResetResult.Fail("Recovery key er format bhul.");
        }

        if (!_hasher.Verify(normalizedKey, record.RecoveryHash))
        {
            await _activity.LogAsync(
                ActivityEventType.Recovery, ActivityOutcome.Failure,
                "Recovery rejected: invalid recovery key.",
                cancellationToken: cancellationToken);

            return RecoveryResetResult.Fail("Recovery key mileni.");
        }

        var validation = PasswordPolicy.Validate(newSecret, newKind);
        if (!validation.IsValid)
        {
            return RecoveryResetResult.Fail(validation.Error!);
        }

        var newRecoveryKey = RecoveryKey.Generate();
        var canonicalNewRecoveryKey = NormalizeRecoveryKeyOrThrow(newRecoveryKey);

        var updated = record with
        {
            Kind = newKind,
            SecretHash = _hasher.Hash(newSecret),
            RecoveryHash = _hasher.Hash(canonicalNewRecoveryKey),
            UpdatedUtc = DateTimeOffset.UtcNow,
        };

        await _repository.SaveAsync(updated, cancellationToken);

        if (_lockout is not null)
        {
            await _lockout.ResetAsync(cancellationToken);
        }

        await _activity.LogAsync(
            ActivityEventType.Recovery, ActivityOutcome.Success,
            $"Credential reset via recovery key; new {newKind} credential set.",
            cancellationToken: cancellationToken);

        return RecoveryResetResult.Ok(newRecoveryKey);
    }

    private static string NormalizeRecoveryKeyOrThrow(string recoveryKey)
    {
        if (!RecoveryKey.TryNormalize(recoveryKey, out var normalized))
        {
            throw new InvalidOperationException("Generated recovery key could not be normalized.");
        }

        return normalized;
    }
}
