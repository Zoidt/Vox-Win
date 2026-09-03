# Initial local inference baseline

Measured on 2026-09-03 on the development PC: Ryzen 7 3700X, 32 GB RAM, Windows 11 x64, .NET 10.0.9. Inference uses sherpa-onnx 1.13.5, CPU provider, four threads, Parakeet TDT 0.6B v3 INT8 from the pinned model revision in `ModelStore`.

The maintainer's `test_wavs/en.wav` contains 3.85 seconds of English speech. The recognizer produced the expected intelligible English sentence on all three runs:

| Run | Decode time |
| --- | --- |
| 1 | 293 ms |
| 2 | 347 ms |
| 3 | 324 ms |

Model initialization took 3.84 seconds after checksum verification. During a five-second resident idle observation following inference, the probe process averaged 0.37% of total machine CPU and had a 798 MiB working set.

These are an initial sample, not a latency guarantee or an accuracy benchmark. They exclude microphone startup, recording time, conversion from a live microphone, and clipboard insertion. The idle interval is short and follows inference. GPU inference has not been implemented or compared.

Run `dotnet run --project tools/Vox.Probe -c Release -- --download path/to/audio.wav` to download/verify the model and benchmark a mono or stereo WAV file. Omit `--download` to verify existing local files without downloading. The probe never opens a microphone and only prints transcripts when explicitly given a file.
