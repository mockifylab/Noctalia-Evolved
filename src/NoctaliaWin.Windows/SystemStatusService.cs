// Battery and network state from documented Windows APIs: GetSystemPowerStatus
// (kernel32) for power, and System.Net.NetworkInformation for connectivity.
//
// Deliberately not used: WMI (extra dependency, slower) and the Network List
// Manager COM APIs (need per-endpoint attribution that Noctalia's widget does not).
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using NoctaliaWin.Core.Abstractions;
using NoctaliaWin.Windows.Native;

namespace NoctaliaWin.Windows;

public sealed class SystemStatusService : ISystemStatusService, IDisposable
{
    // SYSTEM_POWER_STATUS.BatteryFlag values documented by Microsoft.
    private const byte BatteryFlagCharging = 0x08;
    private const byte BatteryFlagNoBattery = 0x80;
    private const byte BatteryPercentageUnknown = 0xFF;

    private readonly Func<SystemStatusSnapshot>? _reader; // test seam
    private readonly Timer? _timer;
    private SystemStatusSnapshot _current;

    public event Action? Changed;

    public SystemStatusService(TimeSpan? pollInterval = null)
        : this(reader: null, pollInterval)
    {
    }

    internal SystemStatusService(Func<SystemStatusSnapshot>? reader, TimeSpan? pollInterval = null)
    {
        _reader = reader;
        _current = Read();
        if (_reader is null)
        {
            var interval = pollInterval ?? TimeSpan.FromSeconds(5);
            _timer = new Timer(_ => Poll(), null, interval, interval);
        }
    }

    public SystemStatusSnapshot Snapshot => _current;

    internal void Poll()
    {
        var next = Read();
        if (next == _current) return;
        _current = next;
        Changed?.Invoke();
    }

    private SystemStatusSnapshot Read() => _reader?.Invoke() ?? ReadLive();

    internal static SystemStatusSnapshot ReadLive()
    {
        int? percent = null;
        var battery = BatteryStatus.NoBattery;

        if (NativeMethods.GetSystemPowerStatus(out var power))
        {
            if (power.BatteryLifePercent != BatteryPercentageUnknown)
                percent = Math.Clamp((int)power.BatteryLifePercent, 0, 100);
            battery = MapBatteryFlag(power.BatteryFlag, percent);
        }

        var (connected, kind) = ReadNetwork();
        return new SystemStatusSnapshot(percent, battery, connected, kind);
    }

    /// The documented BatteryFlag bits decide the state; a flat 100% with no
    /// charging bit is how a full battery reports.
    internal static BatteryStatus MapBatteryFlag(byte batteryFlag, int? percent) =>
        (batteryFlag & BatteryFlagNoBattery) != 0
            ? BatteryStatus.NoBattery
            : (batteryFlag & BatteryFlagCharging) != 0
                ? BatteryStatus.Charging
                : percent >= 100
                    ? BatteryStatus.FullyCharged
                    : percent is null
                        ? BatteryStatus.NoBattery
                        : BatteryStatus.Discharging;

    private static (bool Connected, string? Kind) ReadNetwork()
    {
        bool connected = false;
        string? kind = null;

        foreach (var iface in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (iface.OperationalStatus != OperationalStatus.Up) continue;
            if (iface.NetworkInterfaceType is NetworkInterfaceType.Loopback
                or NetworkInterfaceType.Tunnel) continue;

            // An interface is only "the network" if it actually has an IPv4 default route.
            var properties = iface.GetIPProperties();
            bool hasGateway = properties.GatewayAddresses.Any(g =>
                g.Address is not null && !g.Address.IsIPv6LinkLocal && !g.Address.Equals(System.Net.IPAddress.None));
            bool hasAddress = properties.UnicastAddresses.Any(a =>
                a.Address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
                && !System.Net.IPAddress.IsLoopback(a.Address));
            if (!hasGateway && !hasAddress) continue;

            connected = true;
            kind ??= MapKind(iface.NetworkInterfaceType);
            if (kind == "wifi") break; // Wi-Fi is the more interesting label for the bar
        }

        return (connected, kind);
    }

    internal static string MapKind(NetworkInterfaceType type) => type switch
    {
        // Wireless80216 (WiMAX) is deliberately not labelled "wifi".
        NetworkInterfaceType.Wireless80211 => "wifi",
        NetworkInterfaceType.Ethernet or NetworkInterfaceType.GigabitEthernet
            or NetworkInterfaceType.FastEthernetT or NetworkInterfaceType.FastEthernetFx => "ethernet",
        NetworkInterfaceType.Wwanpp or NetworkInterfaceType.Wwanpp2 => "cellular",
        _ => "other",
    };

    public void Dispose() => _timer?.Dispose();
}
