using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Vox.Windows;

public sealed record MicrophoneOption(string? Id, string Name);

/// <summary>Event-driven WASAPI capture; all audio remains in memory.</summary>
public sealed class MicrophoneCapture : IDisposable
{
    public const int MaximumSeconds = 120;
    private readonly MMDeviceEnumerator _enumerator = new();
    private readonly MMDevice _device;
    private readonly WasapiCapture _capture;
    private readonly MemoryStream _audio = new();
    private readonly TaskCompletionSource _stopped = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private bool _limitReported;
    private bool _disposed;
    private bool _stopRequested;
    public event Action? LimitReached;
    public event Action<string>? DeviceFailed;

    public static IReadOnlyList<MicrophoneOption> GetDevices()
    {
        var result = new List<MicrophoneOption> { new(null, "System default") };
        using var enumerator = new MMDeviceEnumerator();
        foreach (var device in enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
        {
            using (device) result.Add(new(device.ID, device.FriendlyName));
        }
        return result;
    }

    public MicrophoneCapture(string? deviceId)
    {
        try
        {
            _device = deviceId is null
                ? _enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications)
                : _enumerator.GetDevice(deviceId);
            try { _capture = new WasapiCapture(_device, true, 30); }
            catch { _device.Dispose(); throw; }
        }
        catch { _enumerator.Dispose(); throw; }
        _capture.DataAvailable += (_, e) =>
        {
            var remaining = (long)_capture.WaveFormat.AverageBytesPerSecond * MaximumSeconds - _audio.Length;
            var count = (int)Math.Min(e.BytesRecorded, remaining);
            count -= count % _capture.WaveFormat.BlockAlign;
            if (count > 0) _audio.Write(e.Buffer, 0, count);
            if (remaining <= e.BytesRecorded && !_limitReported)
            {
                _limitReported = true;
                LimitReached?.Invoke();
            }
        };
        _capture.RecordingStopped += (_, e) =>
        {
            if (e.Exception is not null) _stopped.TrySetException(e.Exception);
            else _stopped.TrySetResult();
            if (!_stopRequested)
                DeviceFailed?.Invoke("Microphone disconnected or stopped. Choose an available microphone and try again.");
        };
    }

    public void Start() => _capture.StartRecording();

    public async Task<float[]> StopAsync(bool discard)
    {
        _stopRequested = true;
        _capture.StopRecording();
        await _stopped.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (discard) return [];
        _audio.Position = 0;
        using var source = new RawSourceWaveStream(_audio, _capture.WaveFormat);
        ISampleProvider samples = source.ToSampleProvider();
        if (samples.WaveFormat.Channels == 2) samples = new StereoToMonoSampleProvider(samples);
        if (samples.WaveFormat.Channels != 1)
            throw new InvalidOperationException("Select a mono or stereo microphone.");
        samples = new WdlResamplingSampleProvider(samples, 16000);
        var result = new List<float>();
        var buffer = new float[16000];
        int count;
        while ((count = samples.Read(buffer, 0, buffer.Length)) > 0)
            for (var i = 0; i < count; i++) result.Add(buffer[i]);
        var output = result.ToArray();
        System.Runtime.InteropServices.CollectionsMarshal.AsSpan(result).Clear();
        Array.Clear(buffer);
        return output;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _stopRequested = true;
        _capture.Dispose();
        if (_audio.TryGetBuffer(out var segment)) Array.Clear(segment.Array!);
        _audio.Dispose();
        _device.Dispose();
        _enumerator.Dispose();
    }
}
