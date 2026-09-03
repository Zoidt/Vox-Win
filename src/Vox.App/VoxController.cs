using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using Vox.Core;
using Vox.Speech;
using Vox.Windows;

namespace Vox.App;

public sealed class VoxController : INotifyPropertyChanged, IAsyncDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly SettingsStore _store;
    private readonly ModelStore _models = new(ModelStore.DefaultDirectory);
    private readonly ParakeetRecognizer _recognizer = new();
    private readonly TextInsertion _insertion = new();
    private readonly RecordingGesture _gesture = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly DispatcherTimer _tapTimer;
    private readonly DispatcherTimer _clock;
    private GlobalKeyboard? _keyboard;
    private MicrophoneCapture? _capture;
    private CancellationTokenSource? _session;
    private Task _operation = Task.CompletedTask;
    private Task _modelOperation = Task.CompletedTask;
    private nint _target;
    private long _started;
    private bool _processing;
    private bool _modelBusy;
    private bool _disposed;
    private bool _replayNext;
    private readonly bool _preview;
    private string _status = "Preparing Vox…";
    private string _modelStatus = "Parakeet v3 · local speech recognition";
    private string? _lastTranscript;
    private double _modelProgress;
    public event PropertyChangedEventHandler? PropertyChanged;
    public VoxSettings Settings { get; private set; }
    public IReadOnlyList<MicrophoneOption> Microphones { get; private set; } = [new(null, "System default")];
    public string Status => _status;
    public string ModelStatus => _modelStatus;
    public string ShortcutLabel => Settings.Shortcut.Label;
    public string LastSummary => _lastTranscript is null ? "No dictation yet" : "Last dictation available until you quit Vox";
    public bool HasTranscript => _lastTranscript is not null;
    public bool IsReady => _preview || _recognizer.IsReady;
    public bool IsModelBusy => _modelBusy;
    public bool CanConfigure => !_processing && _capture is null && !_modelBusy;
    public bool CanDownload => !_processing && _capture is null && !_modelBusy && !IsReady;
    public bool IsOverlayVisible => _capture is not null || _processing || _replayNext;
    public double ModelProgress => _modelProgress * 100;
    public string OverlayTitle => _replayNext ? "Paste last dictation" : _processing ? "Transcribing…" : _gesture.Mode == RecordingMode.Locked ? "Recording · locked" : "Listening";
    public string OverlayHint
    {
        get
        {
            if (_replayNext) return "Focus a text field, then press your hotkey · Esc cancels";
            if (_processing) return "Esc to discard";
            var elapsed = TimeSpan.FromMilliseconds(Environment.TickCount64 - _started);
            var instruction = _gesture.Mode == RecordingMode.Locked ? "Press hotkey to finish" : "Release to finish";
            return $"{elapsed.Minutes}:{elapsed.Seconds:00}  ·  {instruction}  ·  Esc cancels";
        }
    }
    public string? LastTranscript => _lastTranscript;

    public VoxController(Dispatcher dispatcher, bool preview = false)
    {
        _dispatcher = dispatcher;
        _preview = preview;
        _store = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vox", "settings.json"));
        Settings = preview ? new() : _store.Load();
        _tapTimer = new DispatcherTimer(DispatcherPriority.Input, dispatcher) { Interval = TimeSpan.FromMilliseconds(20) };
        _tapTimer.Tick += (_, _) => { _tapTimer.Stop(); Apply(_gesture.Tick(Environment.TickCount64)); };
        _clock = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromMilliseconds(250) };
        _clock.Tick += (_, _) => Notify(nameof(OverlayHint));
        if (preview) { _status = "Ready to dictate"; _modelStatus = "Ready · kept in memory · CPU"; return; }
        try
        {
            _keyboard = new GlobalKeyboard { Shortcut = Settings.Shortcut };
            _keyboard.ShortcutChanged += down =>
            {
                var target = TextInsertion.ForegroundWindow;
                var timestamp = Environment.TickCount64;
                _dispatcher.BeginInvoke(() => HandleKey(down, timestamp, target), DispatcherPriority.Input);
            };
            _keyboard.CancelPressed += () => _dispatcher.BeginInvoke(Cancel, DispatcherPriority.Input);
            RefreshMicrophones();
            Settings = Settings with { StartWithWindows = StartupRegistration.IsEnabled() };
            SetStatus(_store.LoadWarning ?? "Download the speech model to start.");
        }
        catch (Exception ex) { SetStatus("Setup error: " + ex.Message); }
    }

    public Task InitializeAsync() => _models.IsInstalled ? LoadModelAsync(false) : Task.CompletedTask;
    public Task DownloadModelAsync() => LoadModelAsync(true);

    private Task LoadModelAsync(bool download)
    {
        if (_modelBusy || IsReady || _disposed) return Task.CompletedTask;
        _modelOperation = LoadModelCoreAsync(download);
        return _modelOperation;
    }

    private async Task LoadModelCoreAsync(bool download)
    {
        _modelBusy = true;
        Refresh();
        try
        {
            await _models.EnsureAsync(download, new Progress<ModelProgress>(p =>
            {
                _modelProgress = p.Fraction;
                _modelStatus = p.Message;
                Notify(nameof(ModelProgress)); Notify(nameof(ModelStatus));
            }), _lifetime.Token);
            _modelStatus = "Loading Parakeet into memory…";
            Notify(nameof(ModelStatus));
            await _recognizer.LoadAsync(_models.DirectoryPath, _lifetime.Token);
            _modelStatus = "Ready · kept in memory · CPU";
            SetStatus("Ready to dictate");
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _modelStatus = "Model not ready"; SetStatus("Model setup failed: " + ex.Message); }
        finally { _modelBusy = false; Refresh(); }
    }

    private void HandleKey(bool down, long timestamp, nint target)
    {
        if (_disposed) return;
        if (_replayNext && down)
        {
            _replayNext = false;
            _target = target;
            _operation = ReplayAsync();
            return;
        }
        if (_processing || !IsReady)
        {
            if (!down) _gesture.KeyUp(timestamp);
            if (!IsReady && down) SetStatus("Open Vox from the tray and finish model setup first.");
            return;
        }
        if (down) _target = _capture is null ? target : _target;
        var action = down ? _gesture.KeyDown(timestamp) : _gesture.KeyUp(timestamp);
        if (_gesture.Mode == RecordingMode.AwaitingSecondTap)
        {
            _tapTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(1, _gesture.FinishDeadline - Environment.TickCount64));
            _tapTimer.Start();
        }
        Apply(action);
    }

    private void Apply(RecordingAction action)
    {
        switch (action)
        {
            case RecordingAction.Begin:
                Begin(); break;
            case RecordingAction.Lock:
                _tapTimer.Stop(); SetStatus("Recording locked · press hotkey again to finish"); break;
            case RecordingAction.Finish:
                _tapTimer.Stop(); _operation = FinishAsync(false); break;
        }
        Refresh();
    }

    private void Begin()
    {
        try
        {
            _session?.Dispose();
            _session = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
            _capture = new MicrophoneCapture(Settings.MicrophoneId);
            _capture.LimitReached += () => _dispatcher.BeginInvoke(() =>
            {
                if (_capture is null || _processing) return;
                _gesture.Complete();
                _operation = FinishAsync(false);
            });
            _capture.DeviceFailed += message => _dispatcher.BeginInvoke(() => { Cancel(); SetStatus(message); });
            _started = Environment.TickCount64;
            if (Settings.SoundCues) System.Media.SystemSounds.Asterisk.Play();
            _capture.Start();
            _clock.Start();
            SetStatus("Listening · release to finish, or double-tap to lock");
        }
        catch (Exception ex)
        {
            _capture?.Dispose(); _capture = null;
            _gesture.Cancel();
            SetStatus("Microphone unavailable: " + ex.Message);
        }
    }

    public void Cancel()
    {
        _tapTimer.Stop();
        _replayNext = false;
        _gesture.Cancel();
        _session?.Cancel();
        if (_capture is not null && !_processing) _operation = FinishAsync(true);
        else if (_processing) SetStatus("Cancelling…");
        else SetStatus(IsReady ? "Ready to dictate" : "Model not ready");
        Refresh();
    }

    private async Task FinishAsync(bool discard)
    {
        var capture = _capture;
        if (capture is null || _processing) return;
        _processing = true;
        _clock.Stop();
        Refresh();
        float[] samples = [];
        var previousTranscript = _lastTranscript;
        var token = _session!.Token;
        try
        {
            samples = await capture.StopAsync(discard);
            capture.Dispose(); _capture = null;
            if (discard) { SetStatus("Cancelled · no text inserted"); return; }
            token.ThrowIfCancellationRequested();
            if (Settings.SoundCues) System.Media.SystemSounds.Asterisk.Play();
            SetStatus("Transcribing locally…"); Refresh();
            var result = await _recognizer.TranscribeAsync(samples, 16000, token);
            token.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(result.Text)) { SetStatus("No speech detected. Try again closer to the microphone."); return; }
            _lastTranscript = result.Text;
            Notify(nameof(HasTranscript)); Notify(nameof(LastSummary));
            await _insertion.InsertAsync(result.Text, _target, token);
            SetStatus($"Text sent · transcription {result.Elapsed.TotalMilliseconds:F0} ms");
        }
        catch (OperationCanceledException)
        {
            _lastTranscript = previousTranscript;
            Notify(nameof(HasTranscript)); Notify(nameof(LastSummary));
            SetStatus("Cancelled · no text inserted");
        }
        catch (Exception ex) { SetStatus(ex.Message); }
        finally
        {
            Array.Clear(samples);
            capture.Dispose(); _capture = null;
            _processing = false;
            _gesture.Complete();
            Refresh();
        }
    }

    public void ArmReplay()
    {
        if (_lastTranscript is null || !CanConfigure) return;
        _replayNext = true;
        SetStatus("Focus your text field, then press the dictation hotkey to paste the last transcript.");
        Refresh();
    }

    private async Task ReplayAsync()
    {
        if (_lastTranscript is null) return;
        _processing = true;
        _session?.Dispose();
        _session = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        Refresh();
        try { await _insertion.InsertAsync(_lastTranscript, _target, _session.Token); SetStatus("Last dictation sent"); }
        catch (OperationCanceledException) { SetStatus("Paste cancelled"); }
        catch (Exception ex) { SetStatus(ex.Message); }
        finally { _processing = false; _gesture.Complete(); Refresh(); }
    }

    public void RefreshMicrophones()
    {
        try { Microphones = MicrophoneCapture.GetDevices(); Notify(nameof(Microphones)); }
        catch (Exception) { SetStatus("No microphone is available. Connect one and choose Refresh."); }
    }

    public void SuspendHotkey(bool suspended) { if (_keyboard is not null) _keyboard.Suspended = suspended; }

    public void SaveSettings(VoxSettings updated)
    {
        if (_preview || !CanConfigure || updated == Settings) return;
        try
        {
            if (updated.StartWithWindows != Settings.StartWithWindows) StartupRegistration.SetEnabled(updated.StartWithWindows);
            _store.Save(updated);
            Settings = updated;
            if (_keyboard is not null) _keyboard.Shortcut = updated.Shortcut;
            SetStatus("Settings saved");
        }
        catch (Exception ex) { SetStatus("Could not save settings: " + ex.Message); }
        Notify(nameof(Settings)); Notify(nameof(ShortcutLabel));
    }

    public void SetStatus(string message) { _status = message; Notify(nameof(Status)); }
    private void Refresh()
    {
        if (_keyboard is not null) _keyboard.CanCancel = IsOverlayVisible;
        foreach (var property in new[] { nameof(IsReady), nameof(IsModelBusy), nameof(CanConfigure), nameof(CanDownload), nameof(IsOverlayVisible), nameof(OverlayTitle), nameof(OverlayHint), nameof(ModelStatus) }) Notify(property);
    }
    private void Notify([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _keyboard?.Dispose(); _tapTimer.Stop(); _clock.Stop();
        _lifetime.Cancel(); _session?.Cancel();
        if (_capture is not null && !_processing) _operation = FinishAsync(true);
        await _operation;
        await _modelOperation;
        await _recognizer.DisposeAsync();
        _capture?.Dispose(); _session?.Dispose(); _lifetime.Dispose();
        _lastTranscript = null;
    }
}
