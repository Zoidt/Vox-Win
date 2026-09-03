using System.Text.Json;

namespace Vox.Core;

[Flags]
public enum HotkeyModifiers { None = 0, Control = 1, Alt = 2, Shift = 4, Windows = 8 }

public sealed record Hotkey(int VirtualKey = 0xA3, HotkeyModifiers Modifiers = HotkeyModifiers.None,
    string Label = "Right Ctrl");

public sealed record VoxSettings
{
    public Hotkey Shortcut { get; init; } = new();
    public string? MicrophoneId { get; init; }
    public bool SoundCues { get; init; } = true;
    public bool StartWithWindows { get; init; }
    public TextReplacement[] Replacements { get; init; } = [];
}

public sealed class SettingsStore(string path)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public string? LoadWarning { get; private set; }

    public VoxSettings Load()
    {
        if (!File.Exists(path)) return new();
        try
        {
            var settings = JsonSerializer.Deserialize<VoxSettings>(File.ReadAllText(path), Options)
                ?? throw new JsonException("Empty settings");
            if (settings.Shortcut is null || settings.Shortcut.VirtualKey is < 1 or > 254
                || string.IsNullOrWhiteSpace(settings.Shortcut.Label)
                || ((int)settings.Shortcut.Modifiers & ~15) != 0)
                throw new JsonException("Invalid shortcut");
            try { _ = new TextRewriter(settings.Replacements); }
            catch (ArgumentException)
            {
                LoadWarning = "Text replacements could not be read and are disabled. Your other settings were restored.";
                settings = settings with { Replacements = [] };
            }
            return settings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            LoadWarning = "Settings could not be read. Defaults are in use; saving will replace the unreadable settings.";
            return new();
        }
    }

    public void Save(VoxSettings settings)
    {
        _ = new TextRewriter(settings.Replacements);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        var temporary = path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings, Options));
        File.Move(temporary, path, overwrite: true);
    }
}
