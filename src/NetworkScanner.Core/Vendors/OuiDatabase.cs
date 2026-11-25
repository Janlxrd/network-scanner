using System.Globalization;

namespace NetworkScanner.Core;

public sealed class OuiDatabase
{
    readonly Dictionary<uint, string> entries;

    OuiDatabase(Dictionary<uint, string> entries) => this.entries = entries;

    public static OuiDatabase Parse(TextReader reader)
    {
        var map = new Dictionary<uint, string>();
        while (reader.ReadLine() is { } line)
        {
            var tab = line.IndexOf('\t');
            if (tab != 6 || !uint.TryParse(line.AsSpan(0, 6), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var prefix)) continue;
            var name = line[(tab + 1)..].Trim();
            if (name.Length > 0) map[prefix] = name;
        }
        return new OuiDatabase(map);
    }

    public static OuiDatabase LoadEmbedded()
    {
        using var stream = typeof(OuiDatabase).Assembly.GetManifestResourceStream("NetworkScanner.Core.oui.txt")!;
        using var reader = new StreamReader(stream);
        return Parse(reader);
    }

    public string? Lookup(MacAddress mac) => entries.GetValueOrDefault(mac.Oui);
}
