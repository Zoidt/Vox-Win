using Microsoft.Win32;

namespace Vox.Windows;

public static class StartupRegistration
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        return key?.GetValue("Vox") is string;
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        if (!enabled) { key.DeleteValue("Vox", false); return; }
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Could not locate Vox.exe.");
        if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Launch Vox.exe before enabling start with Windows.");
        key.SetValue("Vox", $"\"{executable}\" --background");
    }
}
