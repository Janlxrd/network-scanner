using System.Globalization;

namespace NetworkScanner.Core;

public readonly record struct MacAddress(ulong Value)
{
    public static MacAddress FromBytes(ReadOnlySpan<byte> b)
    {
        ulong v = 0;
        for (var i = 0; i < 6; i++) v = v << 8 | b[i];
        return new MacAddress(v);
    }

    public static bool TryParse(string s, out MacAddress mac)
    {
        mac = default;
        var parts = s.Trim().Split(':', '-');
        if (parts.Length != 6) return false;
        ulong v = 0;
        foreach (var p in parts)
        {
            if (p.Length != 2 || !byte.TryParse(p, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b)) return false;
            v = v << 8 | b;
        }
        mac = new MacAddress(v);
        return true;
    }

    public uint Oui => (uint)(Value >> 24);

    public bool IsLocallyAdministered => ((Value >> 40) & 0x02) != 0;

    public override string ToString()
    {
        var v = Value;
        return string.Join(":", Enumerable.Range(0, 6).Select(i => ((byte)(v >> (40 - 8 * i))).ToString("X2")));
    }
}
