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
    bool isScanning;

    [ObservableProperty]
    double progress;

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
        var scanned = lastScan is { } t ? $" · scanned at {t.ToLocalTime():HH:mm}" : "";
        NetworkText = $"{network.AdapterName} · {network.Address} · gateway {network.Gateway}{scanned}";
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

    bool HasSelection() => Selected is not null;

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

    bool CanForget() => Selected is { Online: false };

    async Task RunScanAsync()
    {
        if (IsScanning) return;
        if (DetectNetwork()) Show(DeviceMerger.Saved(store, network!));
        if (network is null)
        {
            Devices.Clear();
            StatusText = "";
            return;
        }

        var net = network;
        scanCts = new CancellationTokenSource();
        IsScanning = true;
        Progress = 0;
        var progress = new Progress<ScanProgress>(p =>
        {
            StatusText = p.Stage == Scanner.DoneStage ? StatusText : p.Stage + "...";
            var fraction = p.Total == 0 ? 1 : (double)p.Done / p.Total;
            Progress = p.Stage switch
            {
                Scanner.FindingStage => 0.85 * fraction,
                Scanner.NamesStage => 0.85 + 0.15 * fraction,
                _ => 1,
            };
        });

        try
        {
            var retry = store.Find(null, net.Gateway.ToString(), net.PrefixLength)?.Devices
                .Select(d => System.Net.IPAddress.TryParse(d.Ip, out var ip) ? ip : null)
                .OfType<System.Net.IPAddress>()
                .ToList();
            var result = await new Scanner(new WindowsProbes()).ScanAsync(net, progress, scanCts.Token, retry);
            var statuses = DeviceMerger.Merge(store, result, oui, DateTimeOffset.Now);
            UpstreamRouters = result.UpstreamRouters.Select(a => a.ToString()).ToList();
            UpstreamCount = UpstreamRouters.Count;
            lastScan = DateTimeOffset.Now;
            UpdateNetworkText();
            Show(statuses);
            UpdateCounts(result);
            TrySave();
        }
        catch (Exception e)
        {
            StatusText = "Scan failed: " + e.Message;
        }
        finally
        {
            IsScanning = false;
            scanCts.Dispose();
            scanCts = null;
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
        Devices.Clear();
        foreach (var s in statuses) Devices.Add(new DeviceItem(s));
        Selected = Devices.FirstOrDefault(d => d.Key == selectedKey);
        UpdateCounts(null);
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
    }
}
