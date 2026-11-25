using System.Windows.Media;
using NetworkScanner.Core;

namespace NetworkScanner;

public static class DeviceIcons
{
    public static readonly FontFamily Font = new("Segoe Fluent Icons, Segoe MDL2 Assets");
    public const string Internet = "";

    public static string Glyph(DeviceKind kind) => kind switch
    {
        DeviceKind.Router => "",
        DeviceKind.ThisPc => "",
        DeviceKind.RaspberryPi => "",
        DeviceKind.Computer => "",
        DeviceKind.Phone => "",
        DeviceKind.Tv => "",
        DeviceKind.Speaker => "",
        DeviceKind.Printer => "",
        DeviceKind.Camera => "",
        DeviceKind.GameConsole => "",
        DeviceKind.SmartHome => "",
        DeviceKind.Nas => "",
        _ => "",
    };

    public static string RouterGlyph => Glyph(DeviceKind.Router);

    public static (Color Fill, Color Fore) Tint(DeviceKind kind, bool dark)
    {
        var (darkFill, darkFore, lightFill, lightFore) = kind switch
        {
            DeviceKind.Router => ("#172554", "#93C5FD", "#DBEAFE", "#1D4ED8"),
            DeviceKind.ThisPc or DeviceKind.Computer => ("#2E1065", "#C4B5FD", "#EDE9FE", "#6D28D9"),
            DeviceKind.Phone => ("#422006", "#FCD34D", "#FEF3C7", "#B45309"),
            DeviceKind.Tv => ("#500724", "#F9A8D4", "#FCE7F3", "#BE185D"),
            DeviceKind.Speaker => ("#042F2E", "#5EEAD4", "#CCFBF1", "#0F766E"),
            DeviceKind.Printer => ("#1E293B", "#CBD5E1", "#F1F5F9", "#334155"),
            DeviceKind.Camera => ("#431407", "#FDBA74", "#FFEDD5", "#C2410C"),
            DeviceKind.GameConsole => ("#1E1B4B", "#A5B4FC", "#E0E7FF", "#4338CA"),
            DeviceKind.SmartHome => ("#1A2E05", "#BEF264", "#ECFCCB", "#4D7C0F"),
            DeviceKind.Nas => ("#083344", "#67E8F9", "#CFFAFE", "#0E7490"),
            DeviceKind.RaspberryPi => ("#4C0519", "#FDA4AF", "#FFE4E6", "#BE123C"),
            _ => ("#27272A", "#A1A1AA", "#F4F4F5", "#52525B"),
        };
        return dark ? (Parse(darkFill), Parse(darkFore)) : (Parse(lightFill), Parse(lightFore));
    }

    static Color Parse(string hex) => (Color)ColorConverter.ConvertFromString(hex);

    public static string Label(DeviceKind kind) => kind switch
    {
        DeviceKind.Router => "Router",
        DeviceKind.ThisPc => "This PC",
        DeviceKind.RaspberryPi => "Raspberry Pi",
        DeviceKind.Computer => "Computer",
        DeviceKind.Phone => "Phone",
        DeviceKind.Tv => "TV",
        DeviceKind.Speaker => "Speaker",
        DeviceKind.Printer => "Printer",
        DeviceKind.Camera => "Camera",
        DeviceKind.GameConsole => "Game console",
        DeviceKind.SmartHome => "Smart home",
        DeviceKind.Nas => "NAS",
        _ => "Other",
    };
}
