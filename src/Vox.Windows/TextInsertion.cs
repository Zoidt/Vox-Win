using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Vox.Windows;

public sealed class TextInsertion
{
    public static nint ForegroundWindow => NativeMethods.GetForegroundWindow();
    public static bool IsOwnWindow(nint window)
    {
        NativeMethods.GetWindowThreadProcessId(window, out var process);
        return process == Environment.ProcessId;
    }

    /// <summary>Run on the WPF dispatcher. Never switch focus or overwrite a newer clipboard.</summary>
    public async Task InsertAsync(string text, nint target, CancellationToken token)
    {
        if (target == 0 || IsOwnWindow(target)) throw new InvalidOperationException("Click a text field in another app, then use your dictation shortcut.");
        var deadline = Environment.TickCount64 + 3000;
        while (ModifiersAreDown())
        {
            token.ThrowIfCancellationRequested();
            if (Environment.TickCount64 >= deadline) throw new InvalidOperationException("Release modifier keys, then use Paste last again.");
            await Task.Delay(20, token);
        }
        RequireTarget(target);
        var before = NativeMethods.GetClipboardSequenceNumber();
        var snapshot = SnapshotClipboard();
        if (before != NativeMethods.GetClipboardSequenceNumber())
            throw new InvalidOperationException("Clipboard changed. Use Paste last again.");
        token.ThrowIfCancellationRequested();
        uint temporarySequence = 0;
        try
        {
            var data = new DataObject();
            data.SetData(DataFormats.UnicodeText, text);
            data.SetData("CanIncludeInClipboardHistory", new MemoryStream(BitConverter.GetBytes(0)), false);
            data.SetData("CanUploadToCloudClipboard", new MemoryStream(BitConverter.GetBytes(0)), false);
            Clipboard.SetDataObject(data, false);
            temporarySequence = NativeMethods.GetClipboardSequenceNumber();
            RequireTarget(target);
            token.ThrowIfCancellationRequested();
            NativeMethods.Input[] keys = [NativeMethods.Key(0x11, false), NativeMethods.Key(0x56, false), NativeMethods.Key(0x56, true), NativeMethods.Key(0x11, true)];
            if (NativeMethods.SendInput((uint)keys.Length, keys, Marshal.SizeOf<NativeMethods.Input>()) != keys.Length)
            {
                // Release injected keys even if Windows accepted only part of the sequence.
                NativeMethods.Input[] release = [NativeMethods.Key(0x56, true), NativeMethods.Key(0x11, true)];
                NativeMethods.SendInput(2, release, Marshal.SizeOf<NativeMethods.Input>());
                throw new InvalidOperationException("Windows blocked paste. Use a normal, non-administrator text field or copy the last transcript manually.");
            }
            // Ctrl+V is asynchronous. Give the target time to read the temporary clipboard.
            await Task.Delay(400, CancellationToken.None);
        }
        finally
        {
            if (temporarySequence != 0 && NativeMethods.GetClipboardSequenceNumber() == temporarySequence)
            {
                for (var attempt = 0; ; attempt++)
                {
                    try
                    {
                        if (NativeMethods.GetClipboardSequenceNumber() != temporarySequence) break;
                        if (snapshot is null) Clipboard.Clear();
                        else Clipboard.SetDataObject(snapshot, true);
                        break;
                    }
                    catch (ExternalException) when (attempt < 4) { await Task.Delay(50); }
                }
            }
        }
    }

    private static DataObject? SnapshotClipboard()
    {
        var existing = Clipboard.GetDataObject();
        if (existing is null) return null;
        var snapshot = new DataObject();
        foreach (var format in existing.GetFormats(false))
        {
            var value = existing.GetData(format, false);
            if (value is null) continue;
            object copy = value switch
            {
                MemoryStream stream => new MemoryStream(stream.ToArray()),
                BitmapSource bitmap => bitmap.Clone(),
                byte[] bytes => bytes.Clone(),
                string[] strings => strings.Clone(),
                _ => value
            };
            snapshot.SetData(format, copy, false);
        }
        return snapshot;
    }

    private static bool ModifiersAreDown() =>
        NativeMethods.IsDown(0x10) || NativeMethods.IsDown(0x11) || NativeMethods.IsDown(0x12)
        || NativeMethods.IsDown(0x5B) || NativeMethods.IsDown(0x5C);

    private static void RequireTarget(nint target)
    {
        if (NativeMethods.GetForegroundWindow() != target)
            throw new InvalidOperationException("Focus changed while transcribing. Your text is available through Paste last again.");
    }
}
