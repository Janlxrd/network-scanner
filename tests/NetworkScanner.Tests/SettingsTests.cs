using NetworkScanner.Core;

namespace NetworkScanner.Tests;

public class SettingsTests : IDisposable
{
    readonly string dir = Directory.CreateTempSubdirectory().FullName;
    string P => Path.Combine(dir, "sub", "settings.json");

    public void Dispose() => Directory.Delete(dir, true);

    [Fact]
    public void Load_Missing_DefaultsDark() => Assert.Equal("Dark", AppSettings.Load(P).Theme);

    [Theory]
    [InlineData("{ nope")]
    [InlineData("{\"Theme\": null}")]
    [InlineData("{\"Theme\": \"Purple\"}")]
    [InlineData("null")]
    public void Load_Bad_DefaultsDark(string json)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(P)!);
        File.WriteAllText(P, json);
        Assert.Equal("Dark", AppSettings.Load(P).Theme);
    }

    [Fact]
    public void SaveThenLoad_RoundTrips()
    {
        new AppSettings { Theme = "Light" }.Save(P);
        Assert.Equal("Light", AppSettings.Load(P).Theme);
    }
}
