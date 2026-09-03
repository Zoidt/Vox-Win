# Hands-on acceptance checks

These checks require a real microphone and target applications. They have not been completed by automated testing. Use a scratch document so testing cannot send a message or execute a command unintentionally.

- In Notepad, hold Right Ctrl, dictate an English sentence, and release. Confirm one complete insertion, then compare the text to what you said.
- Double-tap Right Ctrl, release it, dictate, and tap once more. Confirm the recording remains locked after the second release and pastes exactly once.
- Press Escape while holding, while locked, and while waiting after a short tap. Confirm no text is inserted and the next session works.
- Put text on the clipboard before dictation. After insertion, paste manually in a separate scratch field and confirm the original clipboard text was restored. Repeat with the clipboard formats you use, such as copied files or images.
- Switch to another window during transcription. Confirm Vox does not paste there; choose Paste last again and deliberately select your destination.
- Try several consecutive recordings, rapid taps, long holds, silence, and background noise. Confirm there is no stuck recording, repeated insertion, or overlapping transcription.
- Rebind the hotkey to a chord, including any remapped F13-F24 key you use, restart Vox, and verify the binding and microphone preference persist. Hide Vox or focus another ordinary desktop app and confirm the shortcut remains global. Escape should cancel shortcut capture without changing the old binding.
- Disconnect the selected microphone while recording. Confirm an error is visible in settings, audio capture ends, and selecting an available microphone recovers.
- Hide the settings window and verify dictation still works. Launch Vox again and confirm it opens the existing settings window rather than creating a second listener.
- Check your actual apps, especially browser editors and terminals. Test in scratch fields; pasting text can have different consequences in a terminal.
- Quit Vox and verify it leaves no active microphone capture or tray icon. Check Task Manager for idle CPU after loading and after dictation, and note any interference with your other workloads.

The completed automated checks and native inference measurements are documented in `README.md` and `performance.md`. Do not treat a successful build as evidence that every third-party application's paste behavior works.
