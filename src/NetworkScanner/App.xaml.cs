using System.Windows;
using NetworkScanner.Core;

namespace NetworkScanner;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ThemeSwitcher.Apply(AppSettings.Load(AppSettings.DefaultPath).Theme);
    }
}

public static class ThemeSwitcher
{
    public static string Current { get; private set; } = AppSettings.Dark;

    public static event Action? Changed;

    public static void Apply(string theme)
    {
        Current = theme == AppSettings.Light ? AppSettings.Light : AppSettings.Dark;
        var app = Application.Current;
        app.ThemeMode = Current == AppSettings.Light ? ThemeMode.Light : ThemeMode.Dark;
        var merged = app.Resources.MergedDictionaries;
        var old = merged.FirstOrDefault(d => d.Source?.OriginalString.StartsWith("Theme.Dark") == true || d.Source?.OriginalString.StartsWith("Theme.Light") == true);
        var next = new ResourceDictionary { Source = new Uri($"Theme.{Current}.xaml", UriKind.Relative) };
        if (old is null) merged.Insert(0, next);
        else merged[merged.IndexOf(old)] = next;
        Changed?.Invoke();
    }
}
