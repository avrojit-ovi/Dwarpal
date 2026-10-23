using System.Windows.Media;
using Dwarpal.Core.Models;

namespace Dwarpal.App.Services;

public sealed record LockAttemptResult(bool Success, string? Error, int LockoutSeconds = 0);

/// <summary>
/// Lock screen window ke engine er sathe jure dey - window nijei credential verify kore
/// engine er deya delegate diye, ar result e engine process resume kore.
/// </summary>
public sealed record LockScreenContext(
    ProtectedApp App,
    ImageSource? Icon,
    Func<string, Task<LockAttemptResult>> VerifyAsync,
    Func<bool> IsAnyProcessRunning);
