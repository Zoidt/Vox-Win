# Vox

A Windows voice-dictation app inspired by [Hex](https://github.com/kitlangton/Hex), built with C# and WPF using local Parakeet speech recognition.

**Status:** first runnable Windows build. Local inference, the WPF settings window, global shortcuts, in-memory recording, and the tray workflow are implemented. Microphone-to-target-app behavior still needs hands-on verification on your setup.

## Run Vox

Build with the .NET 10 SDK on Windows x64:

```powershell
./scripts/build.ps1
./artifacts/publish/Vox/Vox.exe
```

The script runs the tests and produces a self-contained application plus `artifacts/Vox-win-x64.zip`. Extract the complete `Vox` folder before launching; its DLLs must stay beside `Vox.exe`. The published build does not require a separately installed .NET runtime. It is currently unsigned.

For development, run `dotnet run --project src/Vox.App -c Release`.

1. Open Vox and choose **Download / repair model** if the model is not installed. The one-time download is approximately 670 MB and is verified using pinned SHA-256 checksums.
2. Wait for **Ready to dictate**, then select your microphone or leave **System default**.
3. Click **Shortcut** to record a different key or combination; press Escape to leave it unchanged. The default is **Right Ctrl**.
4. Focus a text field in another application. Hold the shortcut, speak, and release to transcribe and paste.
5. To record hands-free, double-tap the shortcut, then press it once more to finish. Escape cancels.

Close or hide the settings window to keep using Vox from the tray. Open it again from the tray or launch Vox a second time. **Quit** releases the microphone, model, and global keyboard listener.

## Agreed first-release behavior

- Configurable global dictation hotkey.
- Hold the hotkey to record; release it to transcribe and insert the complete result.
- Double-tap the hotkey to lock recording; press it again to finish.
- Escape cancels recording without transcribing or inserting text.
- English dictation initially.
- Insert the completed transcript at the active cursor while preserving the previous clipboard contents.
- A small floating recording indicator and optional start/stop sounds.
- Local transcription after the initial model download, with the model kept initialized while Vox runs.
- Discard recording audio immediately after transcription or cancellation.
- No persistent transcript history. Keep only the last completed transcript in memory for a "Paste last transcript again" command until Vox exits.
- Closing the settings window leaves Vox running in the system tray.
- Optional start with Windows, disabled by default.

## Implementation and current limits

C# and WPF target Windows x64. Parakeet v3 INT8 runs through sherpa-onnx 1.13.5 on the CPU with up to four threads. The initial sample took 293–347 ms to transcribe 3.85 seconds of speech; see [the benchmark and its limits](docs/performance.md). GPU acceleration is not included in this first build.

- A short tap is at most 180 ms, with a 350 ms window for the second tap. A sustained hold submits immediately on release; a lone short tap waits for the double-tap window to close.
- Recordings automatically finish at two minutes in this initial build to bound memory and long-utterance processing.
- The UI supports English dictation. The underlying v3 model auto-detects language; there is no language picker or translation step.
- Paste uses a temporary clipboard and Ctrl+V. The previous clipboard is restored after a brief delay unless you or another app changed it meanwhile. Clipboard restoration and paste compatibility should be checked in your target applications; some clipboard formats and slow apps may behave differently.
- Vox will not switch foreground windows to paste. If focus changes during transcription, use **Paste last again**: focus your intended text field, then press the hotkey. **Copy last** intentionally replaces the clipboard when you choose it.
- Administrator windows, secure prompts, games, and apps that intercept Ctrl+V may reject insertion. Windows may reserve or intercept some shortcuts. Keyboard shortcuts are supported; mouse-button shortcuts are not yet implemented.
- Microphone permission must be enabled for desktop apps in Windows Settings. An unavailable selected microphone produces an error instead of silently choosing a different input.
- The overlay stays on the primary screen. Multiple-monitor positioning and broader accessibility/high-contrast styling remain follow-up work.

## Local data

Settings live in `%LOCALAPPDATA%\Vox\settings.json`; model files live in `%LOCALAPPDATA%\Vox\models\parakeet-v3-int8`. Audio stays in memory and is discarded after completion, failure, or cancellation. Vox does not write transcript history or transcript logs. The last completed transcript remains in process memory until exit. Once the model is installed, normal dictation requires no network connection.

## Verification

```powershell
dotnet test tests/Vox.Core.Tests -c Release
dotnet run --project tools/Vox.Probe -c Release -- --download path/to/sample.wav
```

The tests cover hold/double-tap/cancel timing, repeats and busy input, settings persistence, and corrupt-settings recovery. `Vox.Probe` validates actual native inference using a supplied WAV; it does not capture a microphone. Windows CI builds, tests, and packages the app without downloading the speech model.

For development checks, `Vox.exe --render-preview output.png` renders the actual WPF layout offscreen with example ready state, without activating hotkeys or capturing audio. `Vox.exe --check-startup output.json` verifies the real model and keyboard listener, enumerates microphones without recording, writes a small diagnostic report, then exits. Quit any running Vox instance first.

Before relying on the app, run the [hands-on acceptance checks](docs/acceptance.md).

## Project documents

- [Domain vocabulary](CONTEXT.md)
- [C# and WPF decision](docs/adr/0001-use-csharp-and-wpf.md)
- [Contribution and commit workflow](AGENTS.md)

## Development history

Development uses small, focused commits for coherent changes. See [AGENTS.md](AGENTS.md) for the commit workflow and [third-party notices](THIRD_PARTY_NOTICES.md) for model and library attribution.
