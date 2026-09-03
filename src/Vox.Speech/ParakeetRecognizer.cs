using System.Diagnostics;
using SherpaOnnx;

namespace Vox.Speech;

public sealed record Transcription(string Text, TimeSpan Elapsed);

/// <summary>One resident recognizer; serialize all native calls, including disposal.</summary>
public sealed class ParakeetRecognizer : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1);
    private OfflineRecognizer? _recognizer;
    private bool _disposed;
    public bool IsReady => _recognizer is not null && !_disposed;

    public async Task LoadAsync(string directory, CancellationToken token)
    {
        await _gate.WaitAsync(token);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_recognizer is not null) return;
            _recognizer = await Task.Run(() =>
            {
                var config = new OfflineRecognizerConfig();
                config.FeatConfig.SampleRate = 16000;
                config.FeatConfig.FeatureDim = 80;
                config.ModelConfig.Transducer.Encoder = Path.Combine(directory, "encoder.int8.onnx");
                config.ModelConfig.Transducer.Decoder = Path.Combine(directory, "decoder.int8.onnx");
                config.ModelConfig.Transducer.Joiner = Path.Combine(directory, "joiner.int8.onnx");
                config.ModelConfig.Tokens = Path.Combine(directory, "tokens.txt");
                config.ModelConfig.ModelType = "nemo_transducer";
                config.ModelConfig.Provider = "cpu";
                config.ModelConfig.NumThreads = Math.Clamp(Environment.ProcessorCount / 2, 1, 4);
                config.ModelConfig.Debug = 0;
                return new OfflineRecognizer(config);
            }, token);
        }
        finally { _gate.Release(); }
    }

    public async Task<Transcription> TranscribeAsync(float[] samples, int sampleRate, CancellationToken token)
    {
        await _gate.WaitAsync(token);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var recognizer = _recognizer ?? throw new InvalidOperationException("Speech model is not loaded.");
            if (samples.Length < sampleRate / 6 || samples.All(sample => Math.Abs(sample) < 0.0001f))
                return new("", TimeSpan.Zero);
            return await Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();
                var watch = Stopwatch.StartNew();
                using var stream = recognizer.CreateStream();
                stream.AcceptWaveform(sampleRate, samples);
                recognizer.Decode(stream);
                token.ThrowIfCancellationRequested(); // Native decode cannot be interrupted; never deliver cancelled output.
                return new Transcription(stream.Result.Text.Trim(), watch.Elapsed);
            }, token);
        }
        finally { _gate.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_disposed) return;
            _disposed = true;
            _recognizer?.Dispose();
            _recognizer = null;
        }
        finally { _gate.Release(); }
    }
}
