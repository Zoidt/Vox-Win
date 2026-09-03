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
            MicrophoneId = "test-endpoint", SoundCues = false, StartWithWindows = true,
            Replacements = [new("open ai", "OpenAI"), new("vox", "Vox")]
        };
        new SettingsStore(SettingsPath).Save(settings);
        Assert.Equivalent(settings, new SettingsStore(SettingsPath).Load());
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
        Assert.Equivalent(new VoxSettings(), store.Load());
        Assert.NotNull(store.LoadWarning);
        Assert.Equal(contents, File.ReadAllText(SettingsPath));
    }

    [Fact]
    public void OldSettingsLoadWithoutReplacementRules()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(SettingsPath, "{\"Shortcut\": {\"VirtualKey\": 124, \"Modifiers\": 2, \"Label\": \"Alt + F13\"}}");
        var settings = new SettingsStore(SettingsPath).Load();
        Assert.Equal(124, settings.Shortcut.VirtualKey);
        Assert.Empty(settings.Replacements);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[{\"From\":\"\",\"To\":\"word\"}]")]
    public void InvalidRulesDoNotResetTheShortcut(string rules)
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(SettingsPath, "{\"Shortcut\":{\"VirtualKey\":124,\"Label\":\"F13\"},\"Replacements\":" + rules + "}");
        var store = new SettingsStore(SettingsPath);
        var settings = store.Load();
        Assert.Equal(124, settings.Shortcut.VirtualKey);
        Assert.Empty(settings.Replacements);
        Assert.NotNull(store.LoadWarning);
    }

    public void Dispose() { if (Directory.Exists(_folder)) Directory.Delete(_folder, true); }
}
