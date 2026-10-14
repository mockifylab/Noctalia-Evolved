using NoctaliaWin.Core.Abstractions;
using NoctaliaWin.Windows.Native;

namespace NoctaliaWin.Windows;

/// The COM endpoint behind <see cref="AudioService"/>, extracted so the service's
/// rounding, caching and change notification can be tested without a sound device.
internal interface IAudioVolumeBackend
{
    double? ReadMasterScalar();
    bool ReadMuted();
    bool WriteMasterScalar(double scalar);
    bool WriteMuted(bool muted);
}

/// <summary>
/// Master volume of the default output device. Windows has no managed volume API, so
/// this reads the documented Core Audio endpoint interface and polls it, because the
/// user can move the mixer slider at any moment outside our app.
/// </summary>
public sealed class AudioService : IAudioService, IDisposable
{
    private readonly IAudioVolumeBackend _backend;
    private readonly System.Threading.Timer? _timer;
    private int? _percent;
    private bool _muted;

    public event Action? Changed;

    public AudioService()
        : this(new AudioEndpointVolume(), TimeSpan.FromSeconds(1))
    {
    }

    internal AudioService(IAudioVolumeBackend backend, TimeSpan? pollInterval = null)
    {
        _backend = backend;
        Poll();
        if (pollInterval is { } interval)
            _timer = new System.Threading.Timer(_ => Poll(), null, interval, interval);
    }

    public int? MasterVolumePercent => _percent;

    public bool Muted => _muted;

    public void SetMasterVolume(int percent)
    {
        var clamped = Math.Clamp(percent, 0, 100);
        if (!_backend.WriteMasterScalar(clamped / 100.0)) return;
        _percent = clamped;
        Changed?.Invoke();
    }

    public void ToggleMute()
    {
        if (!_backend.WriteMuted(!_muted)) return;
        _muted = !_muted;
        Changed?.Invoke();
    }

    internal void Poll()
    {
        var scalar = _backend.ReadMasterScalar();
        var percent = scalar is null ? (int?)null : PercentFromScalar(scalar.Value);
        var muted = scalar is not null && _backend.ReadMuted();

        if (percent == _percent && muted == _muted) return;
        _percent = percent;
        _muted = muted;
        Changed?.Invoke();
    }

    /// A 0..1 scalar maps linearly to the percentage the Windows mixer shows.
    internal static int PercentFromScalar(double scalar) =>
        Math.Clamp((int)Math.Round(scalar * 100, MidpointRounding.AwayFromZero), 0, 100);

    public void Dispose() => _timer?.Dispose();
}
