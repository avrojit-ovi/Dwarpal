using Dwarpal.Core.Security;
using Dwarpal.Core.Services;
using Serilog;
using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage.Streams;

namespace Dwarpal.App.Services;

/// <summary>
/// Windows.Media.Capture based intruder selfie. Camera na thakle, privacy block thakle ba
/// capture fail korle null return kore - credential flow kokhono atke na.
/// </summary>
public sealed class CameraSelfieService : IIntruderSelfieService
{
    private readonly AppSettingsService _settings;
    private readonly SelfieStore _store;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public CameraSelfieService(AppSettingsService settings, SelfieStore store)
    {
        _settings = settings;
        _store = store;
    }

    public async Task<string?> CaptureAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            if (!await _settings.GetSelfieEnabledAsync(cancellationToken))
            {
                return null;
            }

            await _gate.WaitAsync(cancellationToken);

            try
            {
                var jpeg = await CaptureJpegAsync();

                return jpeg is null or { Length: < 256 }
                    ? null
                    : await _store.SaveAsync(jpeg, cancellationToken);
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Intruder selfie unavailable");
            return null;
        }
    }

    private static async Task<byte[]?> CaptureJpegAsync()
    {
        MediaCapture? capture = null;

        try
        {
            capture = new MediaCapture();

            await capture.InitializeAsync(new MediaCaptureInitializationSettings
            {
                StreamingCaptureMode = StreamingCaptureMode.Video,
                PhotoCaptureSource = PhotoCaptureSource.VideoPreview,
            });

            using var stream = new InMemoryRandomAccessStream();
            await capture.CapturePhotoToStreamAsync(ImageEncodingProperties.CreateJpeg(), stream);

            if (stream.Size == 0)
            {
                return null;
            }

            stream.Seek(0);

            using var reader = new DataReader(stream.GetInputStreamAt(0));
            await reader.LoadAsync((uint)stream.Size);

            var bytes = new byte[(int)stream.Size];
            reader.ReadBytes(bytes);

            return bytes;
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Camera capture failed");
            return null;
        }
        finally
        {
            capture?.Dispose();
        }
    }
}
