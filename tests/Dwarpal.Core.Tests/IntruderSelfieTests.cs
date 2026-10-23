using System.Text;
using Dwarpal.Core;
using Dwarpal.Core.Models;
using Dwarpal.Core.Security;
using Dwarpal.Core.Services;

namespace Dwarpal.Core.Tests;

public class IntruderSelfieTests
{
    [Fact]
    public async Task SelfieStore_RoundTripsEncryptedJpeg()
    {
        var root = Path.Combine(Path.GetTempPath(), "dwarpal-selfie-tests", Guid.NewGuid().ToString("N"));
        var paths = new AppPaths(root, hardenAcl: false);
        var store = new SelfieStore(paths);
        var jpeg = Encoding.UTF8.GetBytes("not-a-real-jpeg-but-round-trips");

        try
        {
            var path = await store.SaveAsync(jpeg);

            Assert.True(File.Exists(path));
            Assert.True(store.Exists(path));

            // Disk e plaintext jpeg thaka jabe na.
            var onDisk = await File.ReadAllBytesAsync(path);
            Assert.NotEqual(jpeg, onDisk);

            Assert.Equal(jpeg, store.Load(path));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void SelfieStore_RejectsPathOutsideSelfieDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), "dwarpal-selfie-tests", Guid.NewGuid().ToString("N"));
        var store = new SelfieStore(new AppPaths(root, hardenAcl: false));

        try
        {
            Assert.Throws<ArgumentException>(() => store.Load(Path.Combine(root, "dwarpal.db")));
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ThirdWrongAttempt_CapturesOneSelfie_AndLogsPath()
    {
        var fake = new FakeSelfieService();
        await using var fixture = await TestDatabase.CreateAsync(selfies: fake);
        await fixture.CredentialService.SetupAsync(CredentialKind.Pin, "4821");

        await fixture.CredentialService.VerifyAsync("0000", "App unlock");
        await fixture.CredentialService.VerifyAsync("0000", "App unlock");
        Assert.Equal(0, fake.Calls);

        await fixture.CredentialService.VerifyAsync("0000", "App unlock");
        Assert.Equal(1, fake.Calls);

        await fixture.CredentialService.VerifyAsync("0000", "App unlock");
        Assert.Equal(1, fake.Calls);

        // Panchom fail e lockout + arekta selfie.
        var final = await fixture.CredentialService.VerifyAsync("0000", "App unlock");
        Assert.True(final.LockoutSeconds > 0);
        Assert.Equal(2, fake.Calls);

        var entries = await fixture.Activity.QueryAsync(limit: 50);
        Assert.Contains(entries, entry => entry.SelfiePath == FakeSelfieService.Path);
    }

    [Fact]
    public async Task SelfieFailure_DoesNotBreakUnlockFlow()
    {
        await using var fixture = await TestDatabase.CreateAsync(selfies: new ThrowingSelfieService());
        await fixture.CredentialService.SetupAsync(CredentialKind.Pin, "4821");

        var result = await fixture.CredentialService.VerifyAsync("0000", "App unlock");

        Assert.False(result.Success);
        Assert.Contains("baki", result.Error);
    }

    private sealed class FakeSelfieService : IIntruderSelfieService
    {
        public const string Path = @"C:\ProgramData\Dwarpal\selfies\intruder-test.bin";

        public int Calls { get; private set; }

        public Task<string?> CaptureAsync(CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult<string?>(Path);
        }
    }

    private sealed class ThrowingSelfieService : IIntruderSelfieService
    {
        public Task<string?> CaptureAsync(CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Camera unavailable");
    }
}
