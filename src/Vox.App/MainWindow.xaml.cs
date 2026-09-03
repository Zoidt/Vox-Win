using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using Vox.Core;
using Vox.Windows;

namespace Vox.App;

public partial class MainWindow : Window
{
    private readonly VoxController _controller;
    private bool _loading = true;
    private bool _capturing;
    private Key _modifierCandidate;

    public MainWindow(VoxController controller)
    {
        _controller = controller;
        InitializeComponent();
        DataContext = controller;
        ApplyPreferences();
        PreviewKeyDown += CaptureKeyDown;
        PreviewKeyUp += CaptureKeyUp;
        Deactivated += (_, _) => EndCapture(null);
    }

    private void ApplyPreferences()
    {
        _loading = true;
        SoundCheck.IsChecked = _controller.Settings.SoundCues;
        StartupCheck.IsChecked = _controller.Settings.StartWithWindows;
        MicrophoneBox.SelectedItem = _controller.Microphones.FirstOrDefault(m => m.Id == _controller.Settings.MicrophoneId)
            ?? _controller.Microphones[0];
        _loading = false;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        EndCapture(null);
        e.Cancel = true;
        Hide();
        base.OnClosing(e);
    }

    private void CaptureShortcut(object sender, RoutedEventArgs e)
    {
        _capturing = true; _modifierCandidate = Key.None;
        _controller.SuspendHotkey(true);
        ShortcutButton.Content = "Press a key… Esc cancels";
        ShortcutButton.Focus();
    }

    private static bool IsModifier(Key key) => key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin;
    private void CaptureKeyDown(object sender, KeyEventArgs e)
    {
        if (!_capturing) return;
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape) { EndCapture(null); return; }
        if (IsModifier(key)) { _modifierCandidate = key; return; }
        if (key is Key.None or Key.ImeProcessed or Key.DeadCharProcessed) return;
        var modifiers = HotkeyModifiers.None;
        var labels = new List<string>();
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { modifiers |= HotkeyModifiers.Control; labels.Add("Ctrl"); }
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Alt)) { modifiers |= HotkeyModifiers.Alt; labels.Add("Alt"); }
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) { modifiers |= HotkeyModifiers.Shift; labels.Add("Shift"); }
        if (Keyboard.Modifiers.HasFlag(ModifierKeys.Windows)) { modifiers |= HotkeyModifiers.Windows; labels.Add("Win"); }
        labels.Add(key.ToString());
        EndCapture(new(KeyInterop.VirtualKeyFromKey(key), modifiers, string.Join(" + ", labels)));
    }

    private void CaptureKeyUp(object sender, KeyEventArgs e)
    {
        if (!_capturing) return;
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key != _modifierCandidate) return;
        var label = key switch
        {
            Key.LeftCtrl => "Left Ctrl", Key.RightCtrl => "Right Ctrl", Key.LeftAlt => "Left Alt", Key.RightAlt => "Right Alt",
            Key.LeftShift => "Left Shift", Key.RightShift => "Right Shift", Key.LWin => "Left Win", Key.RWin => "Right Win", _ => key.ToString()
        };
        EndCapture(new(KeyInterop.VirtualKeyFromKey(key), HotkeyModifiers.None, label));
    }

    private void EndCapture(Hotkey? shortcut)
    {
        if (!_capturing) return;
        _capturing = false;
        if (shortcut is not null) _controller.SaveSettings(_controller.Settings with { Shortcut = shortcut });
        _controller.SuspendHotkey(false);
        ShortcutButton.SetBinding(ContentProperty, new Binding(nameof(VoxController.ShortcutLabel)));
    }

    private void PreferencesChanged(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        _controller.SaveSettings(_controller.Settings with { SoundCues = SoundCheck.IsChecked == true, StartWithWindows = StartupCheck.IsChecked == true });
        ApplyPreferences();
    }
    private void MicrophoneChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (_loading || MicrophoneBox.SelectedItem is not MicrophoneOption option) return;
        _controller.SaveSettings(_controller.Settings with { MicrophoneId = option.Id });
    }
    private void RefreshMicrophones(object sender, RoutedEventArgs e) { _loading = true; _controller.RefreshMicrophones(); ApplyPreferences(); }
    private async void DownloadModel(object sender, RoutedEventArgs e) => await _controller.DownloadModelAsync();
    private void PasteLast(object sender, RoutedEventArgs e) { _controller.ArmReplay(); Hide(); }
    private void CopyLast(object sender, RoutedEventArgs e)
    {
        if (_controller.LastTranscript is not { } text) return;
        try { Clipboard.SetText(text); _controller.SetStatus("Last dictation copied to your clipboard"); }
        catch (ExternalException) { _controller.SetStatus("Clipboard is busy. Try Copy last again."); }
    }
    private void HideWindow(object sender, RoutedEventArgs e) => Hide();
    private async void QuitVox(object sender, RoutedEventArgs e) => await ((App)Application.Current).QuitAsync();
}
