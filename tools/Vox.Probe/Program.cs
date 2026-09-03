using System.Diagnostics;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Vox.Speech;

if (args.Length == 0)
{
    Console.WriteLine("Vox.Probe --download [audio.wav] | audio.wav\nDownloads the pinned local model only when --download is specified. No microphone access.");
    return;
}
using var cancel = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancel.Cancel(); };
try
{
    var store = new ModelStore(ModelStore.DefaultDirectory);
    var last = -1;
    await store.EnsureAsync(args.Contains("--download"), new Progress<ModelProgress>(p =>
    {
        var percent = (int)(p.Fraction * 100);
        if (percent / 10 == last) return;
        last = percent / 10;
        Console.WriteLine($"{percent}% {p.Message}");
    }), cancel.Token);
    await using var recognizer = new ParakeetRecognizer();
    var load = Stopwatch.StartNew();
    await recognizer.LoadAsync(store.DirectoryPath, cancel.Token);
    Console.WriteLine($"Model ready on CPU in {load.Elapsed.TotalSeconds:F2}s.");
    var path = args.FirstOrDefault(arg => arg != "--download");
    if (path is null) return;
    using var wave = new WaveFileReader(path);
    ISampleProvider provider = wave.ToSampleProvider();
    if (provider.WaveFormat.Channels == 2) provider = new StereoToMonoSampleProvider(provider);
    if (provider.WaveFormat.Channels != 1) throw new InvalidDataException("Use a mono or stereo WAV file.");
    provider = new WdlResamplingSampleProvider(provider, 16000);
    var data = new List<float>();
    var buffer = new float[16000];
    int count;
    while ((count = provider.Read(buffer, 0, buffer.Length)) > 0) data.AddRange(buffer.AsSpan(0, count).ToArray());
    var audio = data.ToArray();
    for (var i = 0; i < 3; i++)
    {
        var result = await recognizer.TranscribeAsync(audio, 16000, cancel.Token);
        Console.WriteLine($"Run {i + 1}: {audio.Length / 16000.0:F2}s audio -> {result.Elapsed.TotalMilliseconds:F0}ms: {result.Text}");
        if (string.IsNullOrWhiteSpace(result.Text)) throw new InvalidDataException("Non-silent sample produced an empty transcript.");
    }
    Array.Clear(audio);
    var process = Process.GetCurrentProcess();
    var cpuStart = process.TotalProcessorTime;
    var idleWatch = Stopwatch.StartNew();
    await Task.Delay(5000, cancel.Token);
    process.Refresh();
    Console.WriteLine($"Resident idle: {(process.TotalProcessorTime - cpuStart).TotalMilliseconds / idleWatch.Elapsed.TotalMilliseconds / Environment.ProcessorCount * 100:F2}% total CPU; working set {process.WorkingSet64 / 1024 / 1024} MiB.");
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    Environment.ExitCode = 1;
}
