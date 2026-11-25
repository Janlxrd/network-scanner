using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetworkScanner.Core;

public sealed class KnownDevice
{
    public string Key { get; set; } = "";
    public string Ip { get; set; } = "";
    public string? Mac { get; set; }
    public string Vendor { get; set; } = "";
    public string? Hostname { get; set; }
    public DeviceKind Kind { get; set; }
    public string? CustomName { get; set; }
    public DeviceKind? CustomKind { get; set; }
    public DateTimeOffset FirstSeen { get; set; }
    public DateTimeOffset LastSeen { get; set; }

    [JsonIgnore]
    public DeviceKind EffectiveKind => CustomKind ?? Kind;
}

public sealed class KnownNetwork
{
    public string GatewayMac { get; set; } = "";
    public string GatewayIp { get; set; } = "";
    public int PrefixLength { get; set; }
    public List<KnownDevice> Devices { get; set; } = new();
}

public sealed class DeviceStore
{
    static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public List<KnownNetwork> Networks { get; set; } = new();

    public static string DefaultPath { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NetworkScanner", "devices.json");

    public static DeviceStore Load(string path)
    {
        try
        {
            if (!File.Exists(path)) return new DeviceStore();
            var store = JsonSerializer.Deserialize<DeviceStore>(File.ReadAllText(path), Json) ?? new DeviceStore();
            store.Networks ??= new List<KnownNetwork>();
            store.Networks.RemoveAll(n => n is null);
            foreach (var n in store.Networks)
            {
                n.Devices ??= new List<KnownDevice>();
                n.Devices.RemoveAll(d => d is null);
            }
            return store;
        }
        catch (JsonException)
        {
            try
            {
                File.Move(path, path + ".bad", true);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
            }
            return new DeviceStore();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return new DeviceStore();
        }
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, Json));
        File.Move(tmp, path, true);
    }

    public DeviceStore Copy() => JsonSerializer.Deserialize<DeviceStore>(JsonSerializer.Serialize(this, Json), Json)!;

    public KnownNetwork? Find(string? gatewayMac, string gatewayIp, int prefixLength) =>
        (gatewayMac is null ? null : Networks.FirstOrDefault(n => n.GatewayMac == gatewayMac))
        ?? Networks.FirstOrDefault(n => n.GatewayIp == gatewayIp && n.PrefixLength == prefixLength);
}
