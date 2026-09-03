# Vox

A Windows voice-dictation app inspired by [Hex](https://github.com/kitlangton/Hex), built with C# and WPF using local Parakeet speech recognition.

**Status:** planning and repository setup. There is no runnable application yet.

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

## Technical direction

C# and WPF target Windows first. The transcription runtime and CPU/GPU configuration still need to be selected and benchmarked. Priorities are low dictation latency, low idle CPU use, and accurate recognition. Idle memory consumption is an accepted trade-off for keeping the model ready.

Settings and hotkey timing details remain to be worked out during implementation. This overview describes agreed behavior; it does not claim that any feature has been implemented or benchmarked.

## Project documents

- [Domain vocabulary](CONTEXT.md)
- [C# and WPF decision](docs/adr/0001-use-csharp-and-wpf.md)
- [Contribution and commit workflow](AGENTS.md)

## Development history

Development uses small, focused commits for coherent changes. Application build and run instructions will be added when the first working project is available.
