# 0001: Use C# and WPF

## Status

Accepted

## Context

Vox is intended first as a lightweight Windows voice-dictation application. Its priorities are low dictation latency, low background CPU use, direct Windows integration, and a simple settings interface. The user preferred the Windows-style C# interface concept and has ample memory available for keeping the Parakeet model loaded.

Rust would make it easier to reuse more of the application across Windows, Linux, and macOS. C# with WPF offers a more direct route to the desired Windows interface and system integration. Speech recognition will execute through a native inference runtime, so the language of the application shell is not expected to determine model accuracy and must be benchmarked before making latency claims.

## Decision

Build Vox in C# with WPF and target Windows first. Keep transcription and application logic separated from Windows-specific hotkey, text-insertion, tray, startup, and UI code so the core can be reused or replaced more easily if portability becomes a real requirement.

Keep the Parakeet model initialized while Vox is running. Favor event-driven background behavior so Vox does not continuously capture audio, run inference, or redraw UI while idle.

## Alternatives

- Rust with a cross-platform UI would improve future portability and offer more predictable memory management, at the cost of more Windows UI and integration work for the initial application.
- C# with Avalonia would allow a shared C# UI across Windows, Linux, and macOS, at the cost of giving up WPF's direct Windows focus.
- C++ would provide native control but add build and memory-management complexity without an established latency advantage for this application.

## Consequences

- The first release can focus on Windows conventions and the preferred WPF interface.
- The application will require a supported .NET desktop runtime, either installed separately or bundled when publishing.
- WPF UI code will not run on Linux or macOS.
- A later cross-platform version will require a different UI and platform integration layers, while properly isolated transcription and application logic can remain reusable.
- Actual latency, idle CPU use, memory use, and CPU-versus-GPU performance remain measurements to establish on the target computer.
