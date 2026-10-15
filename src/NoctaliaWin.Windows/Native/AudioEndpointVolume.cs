// Minimal hand-written COM declarations for the Core Audio endpoint volume API.
// IIDs and vtable order are taken from the published Windows SDK headers
// (mmdeviceapi.h, endpointvolume.h). Slots that are not called are still declared,
// because omitting one would shift every method after it.
using System.Runtime.InteropServices;
using NoctaliaWin.Windows;

namespace NoctaliaWin.Windows.Native;

internal static class CoreAudioIds
{
    public const uint ClsCtxInProcServer = 0x1;
    public const int ERender = 0;     // EDataRole: output devices
    public const int EConsole = 0;    // EEndpointFlow: the "console" stream
}

[ComImport]
[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    [PreserveSig]
    int NotImpl_EnumAudioEndpoints(int dataFlow, uint stateMask, out IntPtr devices);

    [PreserveSig]
    int GetDefaultAudioEndpoint(
        int dataFlow,
        int role,
        [MarshalAs(UnmanagedType.Interface)] out IMMDevice device);

    [PreserveSig] int NotImpl_GetDevice(IntPtr endpointId, out IntPtr device);
    [PreserveSig] int NotImpl_RegisterEndpointNotificationCallback(IntPtr client);
    [PreserveSig] int NotImpl_UnregisterEndpointNotificationCallback(IntPtr client);
}

[ComImport]
[Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
[ClassInterface(ClassInterfaceType.None)]
internal class DeviceEnumeratorCoclass
{
}

[ComImport]
[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    [PreserveSig]
    int Activate(
        ref Guid iid,
        uint dwClsCtx,
        IntPtr pActivationParams,
        [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);

    [PreserveSig] int NotImpl_OpenPropertyStore(uint dwFlags, out IntPtr pStore);
    [PreserveSig] int NotImpl_GetId(out IntPtr pstrId);
    [PreserveSig] int NotImpl_GetState(out uint pdwState);
}

[ComImport]
[Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolume
{
    [PreserveSig] int NotImpl_RegisterControlChangeNotify(IntPtr callback);
    [PreserveSig] int NotImpl_UnregisterControlChangeNotify(IntPtr callback);
    [PreserveSig] int NotImpl_GetChannelCount(out uint pcChannelCount);
    [PreserveSig] int NotImpl_SetMasterVolumeLevel(float fLevelDB, Guid pguidEventContext);

    [PreserveSig]
    int SetMasterVolumeLevelScalar(float fLevel, Guid eventContext);

    [PreserveSig] int NotImpl_GetMasterVolumeLevel(out float pfLevelDB);

    [PreserveSig]
    int GetMasterVolumeLevelScalar(out float level);

    [PreserveSig] int NotImpl_SetChannelVolumeLevel(uint nChannel, float fLevelDB, Guid pguidEventContext);
    [PreserveSig] int NotImpl_SetChannelVolumeLevelScalar(uint nChannel, float fLevel, Guid pguidEventContext);
    [PreserveSig] int NotImpl_GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
    [PreserveSig] int NotImpl_GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);

    [PreserveSig]
    int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, Guid eventContext);

    [PreserveSig]
    int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
}

/// <summary>
/// The default output endpoint's master volume, as a 0..1 scalar plus a mute flag.
/// Returns null readings when the machine has no audio endpoint.
/// </summary>
public sealed class AudioEndpointVolume : IAudioVolumeBackend, IDisposable
{
    private readonly object? _enumeratorInstance;
    private IMMDeviceEnumerator? _enumerator;
    private IMMDevice? _device;
    private IAudioEndpointVolume? _volume;
    private bool _endpointResolved;

    public AudioEndpointVolume()
    {
        try
        {
            _enumeratorInstance = new DeviceEnumeratorCoclass();
            _enumerator = (IMMDeviceEnumerator)_enumeratorInstance;
        }
        catch (COMException)
        {
            // No audio infrastructure on this machine; every read below reports null.
        }
    }

    /// The endpoint can change when devices are plugged in, so re-resolve after a failure.
    private bool EnsureVolume()
    {
        if (_volume is not null && _endpointResolved) return true;
        _volume = null;
        _device = null;
        _endpointResolved = false;

        if (_enumerator is null) return false;

        try
        {
            if (_enumerator.GetDefaultAudioEndpoint(CoreAudioIds.ERender, CoreAudioIds.EConsole, out var device) != NativeMethods.S_OK)
                return false;

            var iid = typeof(IAudioEndpointVolume).GUID;
            if (device.Activate(ref iid, CoreAudioIds.ClsCtxInProcServer, IntPtr.Zero, out var raw) != NativeMethods.S_OK
                || raw is not IAudioEndpointVolume volume)
                return false;

            _device = device;
            _volume = volume;
            _endpointResolved = true;
            return true;
        }
        catch (COMException)
        {
            return false;
        }
    }

    /// Master volume as a 0..1 scalar, or null when unavailable.
    public double? ReadMasterScalar()
    {
        if (!EnsureVolume()) return null;
        try
        {
            if (_volume!.GetMasterVolumeLevelScalar(out float scalar) != NativeMethods.S_OK)
                return null;
            return scalar < 0 ? 0 : scalar > 1 ? 1 : scalar;
        }
        catch (COMException)
        {
            _endpointResolved = false;
            return null;
        }
    }

    public bool ReadMuted()
    {
        if (!EnsureVolume()) return false;
        try
        {
            if (_volume!.GetMute(out bool muted) != NativeMethods.S_OK) return false;
            return muted;
        }
        catch (COMException)
        {
            _endpointResolved = false;
            return false;
        }
    }

    /// Writing to a device we cannot open is a silent no-op; the UI re-reads the
    /// endpoint and shows the real value.
    public bool WriteMasterScalar(double scalar)
    {
        if (!EnsureVolume()) return false;
        try
        {
            var clamped = scalar < 0 ? 0d : scalar > 1 ? 1d : scalar;
            return _volume!.SetMasterVolumeLevelScalar((float)clamped, Guid.Empty) == NativeMethods.S_OK;
        }
        catch (COMException)
        {
            _endpointResolved = false;
            return false;
        }
    }

    public bool WriteMuted(bool muted)
    {
        if (!EnsureVolume()) return false;
        try
        {
            return _volume!.SetMute(muted, Guid.Empty) == NativeMethods.S_OK;
        }
        catch (COMException)
        {
            _endpointResolved = false;
            return false;
        }
    }

    public void Dispose()
    {
        _volume = null;
        _device = null;
        _enumerator = null;
        if (_enumeratorInstance is not null)
            Marshal.ReleaseComObject(_enumeratorInstance);
    }
}
