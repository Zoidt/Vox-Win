using System.Security.Cryptography;

namespace Vox.Speech;

public sealed record ModelFile(string Name, long Bytes, string Sha256);
public sealed record ModelProgress(string Message, double Fraction);

public sealed class ModelStore(string directory)
{
    public const string Revision = "2bda32ec70b097a55adaa07d9a7173915b43cc78";
    public const string Repository = "csukuangfj/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8";
    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Vox", "models", "parakeet-v3-int8");
    public string DirectoryPath => directory;
    public static IReadOnlyList<ModelFile> Files { get; } =
    [
        new("encoder.int8.onnx", 652184281, "acfc2b4456377e15d04f0243af540b7fe7c992f8d898d751cf134c3a55fd2247"),
        new("decoder.int8.onnx", 11845275, "179e50c43d1a9de79c8a24149a2f9bac6eb5981823f2a2ed88d655b24248db4e"),
        new("joiner.int8.onnx", 6355277, "3164c13fc2821009440d20fcb5fdc78bff28b4db2f8d0f0b329101719c0948b3"),
        new("tokens.txt", 93939, "d58544679ea4bc6ac563d1f545eb7d474bd6cfa467f0a6e2c1dc1c7d37e3c35d")
    ];

    public bool IsInstalled => Files.All(file =>
    {
        var info = new FileInfo(Path.Combine(directory, file.Name));
        return info.Exists && info.Length == file.Bytes;
    });

    public async Task EnsureAsync(bool allowDownload, IProgress<ModelProgress>? progress, CancellationToken token)
    {
        Directory.CreateDirectory(directory);
        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Vox-Win/0.1");
        var total = Files.Sum(file => file.Bytes);
        long done = 0;
        foreach (var file in Files)
        {
            token.ThrowIfCancellationRequested();
            var destination = Path.Combine(directory, file.Name);
            progress?.Report(new("Checking model files…", (double)done / total));
            if (!await MatchesAsync(destination, file, token))
            {
                if (!allowDownload) throw new InvalidDataException("The speech model is missing or damaged. Choose Download / repair model.");
                var temporary = destination + ".partial";
                try
                {
                    using var response = await client.GetAsync(
                        $"https://huggingface.co/{Repository}/resolve/{Revision}/{file.Name}",
                        HttpCompletionOption.ResponseHeadersRead, token);
                    response.EnsureSuccessStatusCode();
                    await using (var source = await response.Content.ReadAsStreamAsync(token))
                    await using (var target = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None, 131072, true))
                    {
                        var buffer = new byte[131072];
                        long received = 0;
                        var lastReport = Environment.TickCount64;
                        int count;
                        while ((count = await source.ReadAsync(buffer, token)) > 0)
                        {
                            received += count;
                            if (received > file.Bytes) throw new InvalidDataException("Unexpected model download size.");
                            await target.WriteAsync(buffer.AsMemory(0, count), token);
                            if (Environment.TickCount64 - lastReport >= 150)
                            {
                                progress?.Report(new($"Downloading model · {(done + received) / 1_000_000} / {total / 1_000_000} MB", (double)(done + received) / total));
                                lastReport = Environment.TickCount64;
                            }
                        }
                    }
                    if (!await MatchesAsync(temporary, file, token))
                        throw new InvalidDataException("Model checksum verification failed. Please retry the download.");
                    File.Move(temporary, destination, true);
                }
                finally
                {
                    if (File.Exists(temporary)) File.Delete(temporary);
                }
            }
            done += file.Bytes;
        }
        progress?.Report(new("Model verified", 1));
    }

    private static async Task<bool> MatchesAsync(string path, ModelFile file, CancellationToken token)
    {
        var info = new FileInfo(path);
        if (!info.Exists || info.Length != file.Bytes) return false;
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 131072, true);
        var hash = await SHA256.HashDataAsync(stream, token);
        return Convert.ToHexString(hash).Equals(file.Sha256, StringComparison.OrdinalIgnoreCase);
    }
}
