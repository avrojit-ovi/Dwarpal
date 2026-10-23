using System.Security.Cryptography;
using System.Text;

namespace Dwarpal.Core.Security;

/// <summary>
/// DPAPI (LocalMachine scope) diye sensitive string encrypt. Service SYSTEM hisebe ar app admin
/// hisebe chaluk - same machine e duijon e unprotect korte parbe. Entropy fixed, tai attacker
/// ke DPAPI key + entropy duitoi lagbe (machine er baire DB copy korle kono kaj hobe na).
/// </summary>
public sealed class DpapiProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("Dwarpal.Protection.v1");

    public string Protect(string plaintext)
    {
        ArgumentNullException.ThrowIfNull(plaintext);

        var payload = ProtectedData.Protect(
            Encoding.UTF8.GetBytes(plaintext),
            Entropy,
            DataProtectionScope.LocalMachine);

        return Convert.ToBase64String(payload);
    }

    public string Unprotect(string protectedBase64)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(protectedBase64);

        var payload = ProtectedData.Unprotect(
            Convert.FromBase64String(protectedBase64),
            Entropy,
            DataProtectionScope.LocalMachine);

        return Encoding.UTF8.GetString(payload);
    }
}
