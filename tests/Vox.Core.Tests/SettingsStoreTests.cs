using Vox.Core;
using Xunit;

namespace Vox.Core.Tests;

public sealed class SettingsStoreTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "vox-settings-test-" + Guid.NewGuid().ToString("N"));
    private string SettingsPath => Path.Combine(_folder, "settings.json");

    [Fact]
    public void PreferencesAndShortcutSurviveRestart()
    {
        var settings = new VoxSettings
        {
            Shortcut = new(0x20, HotkeyModifiers.Control | HotkeyModifiers.Shift, "Ctrl + Shift + Space"),
            MicrophoneId = "test-endpoint", SoundCues = false, StartWithWindows = true
        };
        new SettingsStore(SettingsPath).Save(settings);
        Assert.Equal(settings, new SettingsStore(SettingsPath).Load());
        Assert.False(File.Exists(SettingsPath + ".tmp"));
    }

    [Theory]
    [InlineData("{truncated")]
    [InlineData("{\"Shortcut\": null}")]
    [InlineData("{\"Shortcut\": {\"VirtualKey\": 0}}")]
    [InlineData("{\"Shortcut\": {\"VirtualKey\": 32, \"Modifiers\": 128}}")]
    public void InvalidSettingsFallBackWithoutOverwritingTheOriginal(string contents)
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(SettingsPath, contents);
        var store = new SettingsStore(SettingsPath);
        Assert.Equal(new VoxSettings(), store.Load());
        Assert.NotNull(store.LoadWarning);
        Assert.Equal(contents, File.ReadAllText(SettingsPath));
    }

    public void Dispose() { if (Directory.Exists(_folder)) Directory.Delete(_folder, true); }
}
