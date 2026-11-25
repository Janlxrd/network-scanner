using NetworkScanner.Core;

namespace NetworkScanner.Tests;

public class ClassifierTests
{
    static readonly IPAddress Gw = IPAddress.Parse("192.168.1.1"), Me = IPAddress.Parse("192.168.1.23");

    static MacAddress M(string s)
    {
        MacAddress.TryParse(s, out var m);
        return m;
    }

    [Fact]
    public void Gateway_IsRouter() =>
        Assert.Equal(DeviceKind.Router, DeviceClassifier.Classify(Gw, M("DC:A6:32:00:00:01"), null, null, Gw, Me));

    [Fact]
    public void LocalIp_IsThisPc() =>
        Assert.Equal(DeviceKind.ThisPc, DeviceClassifier.Classify(Me, null, null, null, Gw, Me));

    [Theory]
    [InlineData("2C:CF:67:00:00:01")]
    [InlineData("98:FE:54:00:00:01")]
    [InlineData("B8:27:EB:00:00:01")]
    public void PiOui_IsPi(string mac) =>
        Assert.Equal(DeviceKind.RaspberryPi, DeviceClassifier.Classify(IPAddress.Parse("192.168.1.40"), M(mac), null, null, Gw, Me));

    [Fact]
    public void PiHostname_IsPi() =>
        Assert.Equal(DeviceKind.RaspberryPi, DeviceClassifier.Classify(IPAddress.Parse("192.168.1.41"), M("02:11:22:33:44:55"), "RaspberryPi-kitchen", null, Gw, Me));

    [Fact]
    public void Other_IsDevice() =>
        Assert.Equal(DeviceKind.Device, DeviceClassifier.Classify(IPAddress.Parse("192.168.1.50"), M("00:1A:11:00:00:01"), "thing", null, Gw, Me));

    [Theory]
    [InlineData("raspberrypi.", "raspberrypi")]
    [InlineData("RaspberryPi.local", "RaspberryPi")]
    [InlineData("tv.lan", "tv")]
    [InlineData("  ", null)]
    [InlineData(null, null)]
    [InlineData("192.168.1.9", null)]
    [InlineData("nas.home.arpa", "nas")]
    [InlineData("pc.localdomain.", "pc")]
    public void Normalize_StripsSuffixesAndDots(string? input, string? expected) =>
        Assert.Equal(expected, HostName.Normalize(input));

    [Fact]
    public void Vendor_Texts()
    {
        Assert.Equal("MAC unknown", DeviceText.Vendor(null, null));
        Assert.Equal("Private MAC", DeviceText.Vendor(M("DA:00:00:00:00:01"), "ignored"));
        Assert.Equal("Unknown vendor", DeviceText.Vendor(M("00:00:01:00:00:01"), null));
        Assert.Equal("Intel", DeviceText.Vendor(M("00:00:01:00:00:01"), "Intel"));
    }

    [Fact]
    public void DisplayName_Rules()
    {
        Assert.Equal("This PC", DeviceText.DisplayName(DeviceKind.ThisPc, "desktop-1", "Intel"));
        Assert.Equal("Router", DeviceText.DisplayName(DeviceKind.Router, null, "TP-Link"));
        Assert.Equal("raspberrypi", DeviceText.DisplayName(DeviceKind.RaspberryPi, "raspberrypi", "Raspberry Pi Ltd"));
        Assert.Equal("LG Electronics", DeviceText.DisplayName(DeviceKind.Device, null, "LG Electronics"));
        Assert.Equal("Unknown", DeviceText.DisplayName(DeviceKind.Device, null, "MAC unknown"));
        Assert.Equal("Unknown", DeviceText.DisplayName(DeviceKind.Device, null, "Unknown vendor"));
    }

    static readonly IPAddress Other = IPAddress.Parse("192.168.1.77");
    static readonly MacAddress Public = M("00:1A:11:00:00:77");

    [Theory]
    [InlineData("iPhone-of-Sam", DeviceKind.Phone)]
    [InlineData("Galaxy-S21", DeviceKind.Phone)]
    [InlineData("LGwebOSTV", DeviceKind.Tv)]
    [InlineData("DESKTOP-0BMPMFL", DeviceKind.Computer)]
    [InlineData("Sonos-Kitchen", DeviceKind.Speaker)]
    [InlineData("xbox", DeviceKind.GameConsole)]
    [InlineData("DiskStation", DeviceKind.Nas)]
    [InlineData("shelly1-ABC", DeviceKind.SmartHome)]
    [InlineData("esp_12ab34", DeviceKind.SmartHome)]
    [InlineData("EPSON1234", DeviceKind.Printer)]
    [InlineData("frontdoor-cam", DeviceKind.Camera)]
    public void Hostname_Guesses(string host, DeviceKind expected) =>
        Assert.Equal(expected, DeviceClassifier.Classify(Other, Public, host, null, Gw, Me));

    [Theory]
    [InlineData("Espressif Inc.", DeviceKind.SmartHome)]
    [InlineData("Sonos, Inc.", DeviceKind.Speaker)]
    [InlineData("Seiko Epson Corporation", DeviceKind.Printer)]
    [InlineData("Micro-Star INTL CO., LTD.", DeviceKind.Computer)]
    [InlineData("Xiaomi Communications Co Ltd", DeviceKind.Phone)]
    [InlineData("Hikvision Digital Technology Co.,Ltd.", DeviceKind.Camera)]
    [InlineData("Synology Incorporated", DeviceKind.Nas)]
    [InlineData("Nintendo Co.,Ltd", DeviceKind.GameConsole)]
    [InlineData("LG Electronics (Mobile Communications)", DeviceKind.Tv)]
    [InlineData("Acme Engineering", DeviceKind.Device)]
    public void Vendor_Guesses(string vendor, DeviceKind expected) =>
        Assert.Equal(expected, DeviceClassifier.Classify(Other, Public, null, vendor, Gw, Me));

    [Fact]
    public void Hostname_BeatsVendor() =>
        Assert.Equal(DeviceKind.Tv, DeviceClassifier.Classify(Other, Public, "tv", "Samsung Electronics Co.,Ltd", Gw, Me));

    [Fact]
    public void PrivateMac_NoHints_IsPhone() =>
        Assert.Equal(DeviceKind.Phone, DeviceClassifier.Classify(Other, M("DA:11:22:33:44:55"), null, "Private MAC", Gw, Me));

    [Fact]
    public void PublicMac_UnknownVendor_IsDevice() =>
        Assert.Equal(DeviceKind.Device, DeviceClassifier.Classify(Other, Public, null, "Unknown vendor", Gw, Me));
}
