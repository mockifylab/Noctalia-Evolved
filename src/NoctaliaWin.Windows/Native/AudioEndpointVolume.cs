// Minimal hand-written COM declarations for the Core Audio session/volume APIs,
// transcribed from Microsoft's published interface layout (mmdeviceapi.h,
// endpointvolume.h). Only the slots needed to read and set the master volume are
// used; the intervening declarations exist to keep the vtable order correct.
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
[Guid("A95664D2-9613-4F33-9C10-859786478A87")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    [PreserveSig] int NotImpl_EnumDeviceEndpoints(int DataFlow, out IntPtr pDevices);
    [PreserveSig] int NotImpl_EnumDeviceInputs(int DataFlow, out IntPtr pDevices);
    [PreserveSig] int NotImpl_EnumAllEndpoints(out IntPtr pDevices);
    [PreserveSig] int NotImpl_EnumDeviceClasses(ref Guid deviceInterfaceClass, IntPtr szDeviceID, out IntPtr pDevices);

    [PreserveSig]
    int GetDefaultEndpoint(
        int eDataFlow,
        int eRole,
        [MarshalAs(UnmanagedType.Interface)] out IMMDevice ppDevice);

    [PreserveSig] int NotImpl_RegisterEndpointNotificationCallback(IntPtr pNotify);
    [PreserveSig] int NotImpl_UnregisterEndpointNotificationCallback(IntPtr pNotify);
}

[ComImport]
[Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
[ClassInterface(ClassInterfaceType.None)]
internal class DeviceEnumeratorCoclass
{
}

[ComImport]
[Guid("D666063F-1581-4864-8D3C-4C7D87E5F944")]
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
[Guid("5CDD5769-91A8-43A7-9A2B-7C7A96FC9BB7")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IAudioEndpointVolume
{
    [PreserveSig] int NotImpl_RegisterOnNotify(uint dwCallbackRefCount, IntPtr pCallback);
    [PreserveSig] int NotImpl_UnregisterOnNotify(IntPtr pCallback);
    [PreserveSig] int NotImpl_GetChannelCount(out uint pcChannelCount);
    [PreserveSig] int NotImpl_SetMasterVolumeLevel(float fLevelDB, Guid pguidEventContext);

    [PreserveSig]
    int SetMasterVolumeLevelScalar(float fLevel, Guid pguidEventContext);

    [PreserveSig] int NotImpl_GetMasterVolumeLevel(out float pfLevelDB);

    [PreserveSig]
    int GetMasterVolumeLevelScalar(out float pfLevel);

    [PreserveSig] int NotImpl_SetChannelVolumeLevel(uint nChannel, float fLevelDB, Guid pguidEventContext);
    [PreserveSig] int NotImpl_SetChannelVolumeLevelScalar(uint nChannel, float fLevel, Guid pguidEventContext);
    [PreserveSig] int NotImpl_GetChannelVolumeLevel(uint nChannel, out float pfLevelDB);
    [PreserveSig] int NotImpl_GetChannelVolumeLevelScalar(uint nChannel, out float pfLevel);

    [PreserveSig]
    int SetMute([MarshalAs(UnmanagedType.Bool)] bool bMute, Guid pguidEventContext);

    [PreserveSig]
    int GetMute([MarshalAs(UnmanagedType.Bool)] out bool pbMute);
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
            if (_enumerator.GetDefaultEndpoint(CoreAudioIds.ERender, CoreAudioIds.EConsole, out var device) != NativeMethods.S_OK)
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
