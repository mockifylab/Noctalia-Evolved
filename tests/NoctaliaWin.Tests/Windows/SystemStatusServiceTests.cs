using NoctaliaWin.Core.Abstractions;
using NoctaliaWin.Windows;

namespace NoctaliaWin.Tests.Windows;

public sealed class SystemStatusServiceTests
{
    [Theory]
    [InlineData(0x80, 100, BatteryStatus.NoBattery)]   // desktop: no battery installed
    [InlineData(0x80, null, BatteryStatus.NoBattery)]
    [InlineData(0x08 | 0x01, 42, BatteryStatus.Charging)]
    [InlineData(0x08, null, BatteryStatus.Charging)]   // charging, percentage unknown
    [InlineData(0x01, 100, BatteryStatus.FullyCharged)]
    [InlineData(0x01, 60, BatteryStatus.Discharging)]
    [InlineData(0x00, null, BatteryStatus.NoBattery)]  // percentage 0xFF + no flags
    public void BatteryFlagBits_MapToTheDocumentedStates(
        byte batteryFlag, int? percent, BatteryStatus expected)
    {
        Assert.Equal(expected, SystemStatusService.MapBatteryFlag(batteryFlag, percent));
    }

    private static SystemStatusSnapshot Snapshot(int? percent, BatteryStatus battery, bool net) =>
        new(percent, battery, net, net ? "wifi" : null);

    [Fact]
    public void Snapshot_StartsFromTheInjectedReading()
    {
        var service = new SystemStatusService(
            () => Snapshot(75, BatteryStatus.Discharging, true));

        Assert.Equal(75, service.Snapshot.BatteryPercent);
        Assert.Equal(BatteryStatus.Discharging, service.Snapshot.Battery);
        Assert.True(service.Snapshot.NetworkConnected);
        Assert.Equal("wifi", service.Snapshot.NetworkKind);
    }

    [Fact]
    public void Changed_FiresOnlyWhenSomethingActuallyChanged()
    {
        var current = Snapshot(75, BatteryStatus.Discharging, true);
        var service = new SystemStatusService(() => current);

        int raised = 0;
        service.Changed += () => raised++;

        service.Poll();
        service.Poll();
        Assert.Equal(0, raised);

        current = Snapshot(74, BatteryStatus.Discharging, true);
        service.Poll();
        Assert.Equal(1, raised);
        Assert.Equal(74, service.Snapshot.BatteryPercent);

        current = Snapshot(74, BatteryStatus.Discharging, false);
        service.Poll();
        Assert.Equal(2, raised);
        Assert.Null(service.Snapshot.NetworkKind);
    }

    [Fact]
    public void NetworkKind_MapIsLimitedToHonestLabels()
    {
        Assert.Equal("wifi", SystemStatusService.MapKind(System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211));
        Assert.Equal("ethernet", SystemStatusService.MapKind(System.Net.NetworkInformation.NetworkInterfaceType.GigabitEthernet));
        Assert.Equal("cellular", SystemStatusService.MapKind(System.Net.NetworkInformation.NetworkInterfaceType.Wwanpp));
        Assert.Equal("other", SystemStatusService.MapKind(System.Net.NetworkInformation.NetworkInterfaceType.Loopback));
    }

    [Fact]
    public void ReadLive_OnThisMachineReturnsAConsistentSnapshot()
    {
        // Real GetSystemPowerStatus + real interface enumeration; asserts only the
        // invariants that must hold whatever hardware is present.
        var snapshot = SystemStatusService.ReadLive();

        Assert.True(snapshot.BatteryPercent is null or >= 0 and <= 100);
        Assert.True(snapshot.NetworkConnected || snapshot.NetworkKind is null);
        Assert.True(!snapshot.NetworkConnected || snapshot.NetworkKind is "wifi" or "ethernet" or "cellular" or "other");
    }

    [Fact]
    public void TimerBackedService_IsDisposableAndStartsWithAReading()
    {
        using var service = new SystemStatusService(TimeSpan.FromMilliseconds(50));
        Assert.NotNull(service.Snapshot);
    }
}
