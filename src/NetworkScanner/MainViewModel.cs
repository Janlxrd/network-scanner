using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NetworkScanner.Core;

namespace NetworkScanner;

public sealed record KindOption(DeviceKind? Kind, string Label);

public partial class MainViewModel : ObservableObject
{
    static readonly DeviceKind[] PickableKinds =
    {
        DeviceKind.Phone, DeviceKind.Computer, DeviceKind.Tv, DeviceKind.Speaker, DeviceKind.Printer, DeviceKind.Camera,
        DeviceKind.GameConsole, DeviceKind.SmartHome, DeviceKind.Nas, DeviceKind.RaspberryPi, DeviceKind.Device,
    };

    readonly OuiDatabase oui = OuiDatabase.LoadEmbedded();
    readonly string storePath = DeviceStore.DefaultPath;
    DeviceStore store = new();
    IReadOnlyList<DeviceStatus> shown = Array.Empty<DeviceStatus>();
    LocalNetwork? network;
    CancellationTokenSource? scanCts;
    bool live;
    string? scanError;

    [ObservableProperty]
    string networkText = "";

    [ObservableProperty]
    string networkTitle = "";

    [ObservableProperty]
    int deviceCount;

    [ObservableProperty]
    int onlineCount;

    [ObservableProperty]
    int upstreamCount;

    DateTimeOffset? lastScan;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ScanCommand))]
    bool canScan;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ScanButtonText))]
    [NotifyPropertyChangedFor(nameof(Sweep))]
    [NotifyCanExecuteChangedFor(nameof(ForgetCommand))]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    bool isScanning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Sweep))]
    double progress;

    [ObservableProperty]
    string scanText = "";

    [ObservableProperty]
    bool isEmpty;

    [ObservableProperty]
    string emptyTitle = "";

    [ObservableProperty]
    string emptyText = "";

    [ObservableProperty]
    string? emptyButtonText;

    [ObservableProperty]
    string statusText = "";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ForgetCommand))]
    [NotifyCanExecuteChangedFor(nameof(EditCommand))]
    DeviceItem? selected;

    [ObservableProperty]
    bool isEditing;

    [ObservableProperty]
    string editName = "";

    [ObservableProperty]
    IReadOnlyList<KindOption> editKinds = Array.Empty<KindOption>();

    [ObservableProperty]
    KindOption? editKind;

    [ObservableProperty]
    bool canEditKind;

    [ObservableProperty]
    string themeButtonText = ThemeSwitcher.Current == AppSettings.Dark ? "Light" : "Dark";

    [ObservableProperty]
    IReadOnlyList<string> upstreamRouters = Array.Empty<string>();

    public ObservableCollection<DeviceItem> Devices { get; } = new();

    public string ScanButtonText => IsScanning ? "Stop" : "Scan";

    public double Sweep => IsScanning ? Progress : -1;

    public async Task StartAsync()
    {
        CanScan = true;
        store = DeviceStore.Load(storePath);
        if (DetectNetwork()) Show(DeviceMerger.Saved(store, network!));
        await RunScanAsync();
    }

    bool DetectNetwork()
    {
        var detected = NetworkPicker.Choose(NetworkPicker.ReadSystem());
        if (detected is null)
        {
            network = null;
            NetworkTitle = "No network";
            NetworkText = "No network connection found";
            return false;
        }

        var changed = network is null || !network.Gateway.Equals(detected.Gateway) || !network.Address.Equals(detected.Address);
        network = detected;
        var mask = network.PrefixLength == 0 ? 0u : uint.MaxValue << (32 - network.PrefixLength);
        NetworkTitle = $"{IpUtil.FromUInt32(IpUtil.ToUInt32(network.Address) & mask)}/{network.PrefixLength}";
        UpdateNetworkText();
        if (changed)
        {
            UpstreamRouters = Array.Empty<string>();
            UpstreamCount = 0;
        }
        return changed;
    }

    public void CancelScan() => scanCts?.Cancel();

    void UpdateNetworkText()
    {
        if (network is null) return;
        NetworkText = $"{network.AdapterName} · {network.Address} · gateway {network.Gateway}";
    }

    partial void OnSelectedChanged(DeviceItem? value) => IsEditing = false;

    [RelayCommand]
    void ToggleTheme()
    {
        var next = ThemeSwitcher.Current == AppSettings.Dark ? AppSettings.Light : AppSettings.Dark;
        ThemeSwitcher.Apply(next);
        ThemeButtonText = next == AppSettings.Dark ? "Light" : "Dark";
        Show(shown);
        try
        {
            new AppSettings { Theme = next }.Save(AppSettings.DefaultPath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            StatusText = "Could not save settings: " + e.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    void Edit()
    {
        if (Selected is not { } item) return;
        var d = item.Device;
        EditName = d.CustomName ?? "";
        var options = new List<KindOption> { new(null, $"Automatic ({DeviceIcons.Label(d.Kind)})") };
        options.AddRange(PickableKinds.Select(k => new KindOption(k, DeviceIcons.Label(k))));
        EditKinds = options;
        EditKind = options.FirstOrDefault(o => o.Kind == d.CustomKind) ?? options[0];
        CanEditKind = d.Kind is not (DeviceKind.Router or DeviceKind.ThisPc);
        IsEditing = true;
    }

    bool HasSelection() => Selected is not null && !IsScanning;

    [RelayCommand]
    void SaveEdit()
    {
        if (Selected is not { } item) return;
        DeviceMerger.SetCustom(item.Device, EditName, EditKind?.Kind);
        IsEditing = false;
        Show(shown);
        TrySave();
    }

    [RelayCommand]
    void CancelEdit() => IsEditing = false;

    [RelayCommand(CanExecute = nameof(CanScan))]
    void Scan()
    {
        if (IsScanning) CancelScan();
        else _ = RunScanAsync();
    }

    [RelayCommand(CanExecute = nameof(CanForget))]
    void Forget()
    {
        if (Selected is not { } item || network is null) return;
        var known = store.Find(null, network.Gateway.ToString(), network.PrefixLength);
        known?.Devices.RemoveAll(d => d.Key == item.Key);
        shown = shown.Where(s => s.Device != item.Device).ToList();
        Devices.Remove(item);
        Selected = null;
        UpdateCounts(null);
        TrySave();
    }

    bool CanForget() => Selected is { Online: false } && !IsScanning;

    async Task RunScanAsync()
    {
        if (IsScanning) return;
        scanError = null;
        if (DetectNetwork()) Show(DeviceMerger.Saved(store, network!));
        if (network is null)
        {
            Devices.Clear();
            StatusText = "";
            ScanText = "";
            UpdateEmpty();
            return;
        }

        var net = network;
        scanCts = new CancellationTokenSource();
        IsScanning = true;
        Progress = 0;
        live = true;
        Show(shown);
        ScanText = "Starting scan";
        var clock = Stopwatch.StartNew();
        var found = 0;
        var progress = new Progress<ScanProgress>(p =>
        {
            if (!live) return;
            if (p.Devices is { } snapshot)
            {
                found = snapshot.Count;
                var partial = new ScanResult(net, snapshot, Array.Empty<System.Net.IPAddress>(), false, false);
                Show(DeviceMerger.Preview(store, partial, oui, DateTimeOffset.Now));
            }
            var fraction = p.Total == 0 ? 1 : (double)p.Done / p.Total;
            if (p.Stage == Scanner.FindingStage)
            {
                ScanText = $"Checking {p.Done} of {p.Total} addresses · {found} found";
                Progress = 0.85 * fraction;
            }
            else if (p.Stage == Scanner.NamesStage)
            {
                ScanText = $"Looking up names · {found} found";
                Progress = 0.85 + 0.15 * fraction;
            }
        });

        try
        {
            var retry = store.Find(null, net.Gateway.ToString(), net.PrefixLength)?.Devices
                .Select(d => System.Net.IPAddress.TryParse(d.Ip, out var ip) ? ip : null)
                .OfType<System.Net.IPAddress>()
                .ToList();
            var result = await new Scanner(new WindowsProbes()).ScanAsync(net, progress, scanCts.Token, retry);
            live = false;
            Progress = 1;
            var statuses = DeviceMerger.Merge(store, result, oui, DateTimeOffset.Now);
            UpstreamRouters = result.UpstreamRouters.Select(a => a.ToString()).ToList();
            UpstreamCount = UpstreamRouters.Count;
            lastScan = DateTimeOffset.Now;
            Show(statuses);
            UpdateCounts(result);
            var seconds = clock.Elapsed.TotalSeconds.ToString("0.0", CultureInfo.InvariantCulture);
            var count = result.Devices.Count;
            ScanText = result.Cancelled
                ? $"Stopped after {seconds} s · {count} found"
                : $"Found {count} {(count == 1 ? "device" : "devices")} in {seconds} s · {lastScan.Value.ToLocalTime():HH:mm}";
            TrySave();
        }
        catch (Exception e)
        {
            live = false;
            scanError = e.Message;
            Show(shown);
            StatusText = "Scan failed: " + e.Message;
            ScanText = "";
        }
        finally
        {
            live = false;
            IsScanning = false;
            scanCts.Dispose();
            scanCts = null;
            UpdateEmpty();
        }
    }

    void TrySave()
    {
        try
        {
            store.Save(storePath);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            StatusText = "Could not save devices: " + e.Message;
        }
    }

    void Show(IReadOnlyList<DeviceStatus> statuses)
    {
        shown = statuses;
        var selectedKey = Selected?.Key;
        var items = statuses.Select(s => new DeviceItem(s, live)).ToList();
        var keys = items.Select(i => i.Key).ToHashSet();
        if (keys.Count != items.Count) Devices.Clear();
        for (var i = Devices.Count - 1; i >= 0; i--)
            if (!keys.Contains(Devices[i].Key)) Devices.RemoveAt(i);
        foreach (var item in items)
        {
            var index = Devices.Select(d => d.Key).ToList().IndexOf(item.Key);
            if (index >= 0 && keys.Count == items.Count) Devices[index] = item;
            else Devices.Add(item);
        }
        Selected = Devices.FirstOrDefault(d => d.Key == selectedKey);
        UpdateCounts(null);
    }

    void UpdateEmpty()
    {
        IsEmpty = Devices.Count == 0;
        if (network is null)
        {
            EmptyTitle = "No network connection";
            EmptyText = "Connect to Wi-Fi or Ethernet, then try again.";
            EmptyButtonText = "Try again";
        }
        else if (scanError is not null)
        {
            EmptyTitle = "Scan failed";
            EmptyText = scanError;
            EmptyButtonText = "Try again";
        }
        else if (IsScanning)
        {
            EmptyTitle = "Looking for devices";
            EmptyText = $"Checking every address on {NetworkTitle}.";
            EmptyButtonText = null;
        }
        else
        {
            EmptyTitle = "No devices yet";
            EmptyText = $"Scan to find the devices on {NetworkTitle}.";
            EmptyButtonText = "Scan";
        }
    }

    void UpdateCounts(ScanResult? result)
    {
        var online = Devices.Count(d => d.Online);
        DeviceCount = Devices.Count;
        OnlineCount = online;
        var text = $"{online} online, {Devices.Count - online} offline";
        if (result is { Capped: true } && network is not null)
        {
            var hosts = ScanRange.For(network.Address, network.PrefixLength).Hosts;
            text += $"  (scanned {hosts[0]}-{hosts[^1]} only)";
        }
        if (result is { Cancelled: true }) text += "  (stopped)";
        StatusText = text;
        UpdateEmpty();
    }
}
