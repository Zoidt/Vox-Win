# Working in Vox

Vox is a Windows voice-dictation application using C# and WPF. Read `README.md`, `CONTEXT.md`, and relevant decisions under `docs/adr/` before making changes.

## Commit workflow

The user wants frequent commits organized by change so development is easy to track.

- Commit each coherent, completed change with a descriptive message explaining its purpose.
- Separate unrelated features, fixes, refactors, and documentation work into different commits.
- Use small, reviewable commits without splitting a single working change into artificial fragments.
- Run checks appropriate to each change before committing; describe any verification limitation in the handoff.
- Stage intended files explicitly. Do not include unrelated user changes, model downloads, recordings, credentials, or build output.
- Preserve existing history. Do not amend, squash, or force-push unless the user requests it.

## Product boundaries

- Keep microphone capture and inference inactive while idle; retain the initialized model for responsiveness.
- Keep Windows integration separate from transcription and application logic.
- Do not add cloud transcription, persistent transcript history, or retained dictation audio without a user request.
- The `grill-with-docs` skill is explicit-only. Do not invoke it automatically from repository instructions.
