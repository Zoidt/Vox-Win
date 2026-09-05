# CourtSide AI hackathon MVP plan

Prepared for the next work session, September 5, 2026.

## Objective

Build a two-court demonstrator with a court scorer on an iPad and an organizer dashboard on a laptop. Show voice-driven scoring, tactile correction, doubles rotation, offline scoring with reconnect, and an obvious court-available notification when a match finishes.

This document captures the planning conversation. Implementation has not started. CourtSide should get its own project/repository when work begins; this plan is stored in Vox for continuity and does not change Vox's Windows/C#/WPF architecture.

## Proposed direction and decisions still open

The proposed default is an installed iPad app using React Native, Expo, and TypeScript, with FluidAudio running Parakeet v3 locally. The organizer uses a React/TypeScript web app. Most application code would be TypeScript; the speech library supplies the native Swift implementation.

This is provisional until we establish the available hardware and get a real-device voice sample working. A web-only scorer using Rhino is an alternative to test if avoiding native builds is important. Do not build both application shells during the hackathon.

At the beginning of the session, settle:

- Exact iPad model, iPadOS version, and available storage.
- Access to a Mac with Xcode for local native builds and installation. If using a remote build service instead, prove signing and device installation before relying on it.
- Whether the demo needs both English and French. Default product target: both, with an explicit match language setting where the provider supports it.
- Whether an installed app is acceptable or browser delivery is a priority.
- Available second client and demo network. One iPad plus a laptop running the dashboard and a second scorer is sufficient for the proposed demonstration; do not imply two physical tablets were tested.
- Challenge eligibility and the organizer's required IP acknowledgement. The user handles acknowledgement; do not accept terms on their behalf.

## The demonstration we are building toward

1. Organizer creates a doubles match, chooses a rules preset, establishes player order, and assigns Court 1.
2. Court 1 opens a large landscape scoreboard showing score, current server, relevant receiver information, voice state, and sync status.
3. A player announces a supported score. The app shows what it heard, accepts a valid transition, and updates the organizer dashboard.
4. A repeated announcement does not award an extra point. An invalid or ambiguous announcement asks for clarification and leaves the score intact.
5. A player corrects an error using undo and a tactile point button.
6. Disconnect Court 1. Score several points, restart the app, and verify that the match survives locally.
7. Reconnect. The dashboard receives the missing events once, in order.
8. Show a changeover timer with a preloaded sponsor graphic and an audible time alert.
9. Finish the match. The organizer sees Court 1 become available while Court 2 remains active.

Keep a prepared late-match scenario for the demo so completion does not require playing a full match. Label demo controls clearly.

## Scope

### Required for the MVP

- Landscape court scoreboard with large type, high contrast, clear team/player labels, voice toggle, connection status, point buttons, undo, and correction/override entry.
- Singles and doubles/mixed-doubles player configuration; explicit initial service and receiving order.
- A small, tested set of rules presets covering ordinary advantage scoring, No-Ad, a deciding match tiebreak, and an Express variant with rest disabled. Define each preset exactly before coding it.
- English/French score vocabulary and a small supported command set: score announcement, point to team A/B, correction/undo, and fault.
- Organizer dashboard with two court cards, match setup, assignment, live scores, disconnected/stale status, and match-complete notification.
- Local durable match events and state recovery; reconnect synchronization with duplicate protection.
- Changeover timer, time alert, and one locally available sponsor graphic.
- Voice-provider adapter boundary, one working provider, and a developer view for testing.

### Add only after the core demonstration works

- A second voice provider for comparison.
- Organizer rule pushes with explicit versioning and a safe application boundary.
- A locally cached sponsor video or small sponsor selector.
- Additional score animations and match history views based on scoring events.

### Deferred

Full tournament draws, registration, payments, sponsor billing, rich ad uploads, automated dispute resolution, reliable speaker identity, automatic adjacent-court rejection, cloud transcription, and arbitrary natural-language instructions. The voice MVP is assisted self-scoring, not an automated umpire.

If the deadline forces further cuts, disclose missing challenge features rather than demonstrating them as complete. Protect scoring correctness, correction, live sync, and offline recovery before extra visual features.

## Architecture

| Component | Proposed implementation | Responsibility |
| --- | --- | --- |
| Court client | React Native + Expo + TypeScript | UI, microphone lifecycle, local scoring, durable storage |
| Voice backend | FluidAudio + Parakeet v3 initially | On-device recognition; replaceable adapter |
| Organizer | React + TypeScript | Court overview, setup, assignments, completion alerts |
| Shared domain package | Pure TypeScript | Commands, tennis state transitions, rules presets, validation |
| Court storage | SQLite for native; IndexedDB if web is selected | Events, snapshots, outbox, cached match configuration |
| Sync service | Small Node.js WebSocket service with durable storage | Accept events, acknowledge them, broadcast state, serve catch-up |

Suggested future repository layout:

```text
apps/court/
apps/organizer/
packages/domain/
packages/voice/
packages/protocol/
services/sync/
docs/
```

Choose compatible pinned package versions during setup. Parakeet's documented native route does not run in Safari or standard Expo Go; it requires a native build. Verify the wrapper's actual microphone and model APIs in a small spike before designing around them.

If Rhino proves the better delivery choice, use a React web scorer and keep the domain/protocol boundaries. Browser microphone permissions, HTTPS, foreground behavior, asset caching, and offline initialization must be tested on the iPad. Treat switching application platforms as a separate effort from switching voice providers.

## Replaceable voice providers

Pipeline:

```text
Microphone -> provider adapter -> transcript/intent normalization
           -> scoring command -> domain validation -> persisted event -> UI + sync
```

- Transcription providers such as Parakeet, Apple Speech, and Whisper produce text. A shared language-aware parser maps supported phrases to commands.
- Rhino produces intents and slots. Its adapter maps those to the same command types.
- Keep optional provider confidence as diagnostic data. Scores from different engines are not directly comparable probabilities.
- Keep the domain package independent of provider SDKs, microphone APIs, network clients, and UI frameworks.

Illustrative commands:

```ts
type ScoringCommand =
  | { type: "announceScore"; server: PointValue; receiver: PointValue }
  | { type: "awardPoint"; teamId: string }
  | { type: "undo" }
  | { type: "fault" };
```

Define `PointValue` and score formats for normal games and tiebreaks in the domain implementation. A recognition result must include its utterance/result identity and whether it is final; provisional transcripts never commit score changes.

Each provider needs prepare/load, start, stop/cancel, availability/capability reporting, and dispose operations. Switching providers stops capture, cancels pending work, ignores late results from the previous session, unloads its model, and initializes the replacement. Load one model at a time.

Developer controls:

- Provider selection from engines actually available in the current build.
- Model loading and errors.
- Final recognized text or intent and normalized command.
- End-of-speech-to-result latency and end-of-speech-to-visible-update latency.
- Dry-run mode that never changes the match.
- Brief in-memory diagnostics; no retained dictation audio or transcript logging by default.

Speech capture and inference stop when voice is disabled or the match is inactive. During active hands-free scoring, microphone capture and speech detection necessarily remain active. Keep the selected model initialized for responsiveness where memory allows.

## Model selection experiment

Do not spend the hackathon integrating every candidate. Prove one first and compare at most one other after the scoring loop works.

| Candidate | Reason to test | Validation needed |
| --- | --- | --- |
| Parakeet v3 / FluidAudio | Initial native choice; English/French transcription | Real-device memory, short utterance latency, accent accuracy, wrapper integration |
| Apple SpeechAnalyzer / SpeechTranscriber | Native on-device baseline | Exact device/OS support, supported locales, model assets, integration effort |
| Whisper / WhisperKit | Multilingual comparison if recognition is weak | Model size, latency, memory, short-call accuracy |
| Rhino | Restricted commands; possible web scorer | Command grammar, language-specific assets, Safari behavior, account terms and offline initialization |

Use roughly 40-60 labeled test utterances, balanced across intended demo languages and several speakers if available. Include score pairs, love/zero, deuce/advantage, tiebreak numbers, correction, fault, repeated calls, unrelated speech, and another person calling a plausible score. Test at the actual microphone distances expected in the demonstration.

Measure correct normalized commands, missed commands, wrong scoring actions, false triggers during non-command audio, median/tail response delay, and sustained operation. A short test cannot establish production reliability. Any reproducible wrong scoring action in the demo scenarios needs a guard, clarification path, or narrower command format before release.

Use the same supplied clips for both engines when available, plus live trials; hold test audio in memory unless the user separately chooses to retain recordings. Never claim noise playback reproduces an outdoor court exactly.

Provisional responsiveness target: typical visible update within one second after the call ends. This is a target to measure, not a promise. Decide the demo engine using command correctness and false-update rate before raw speed.

## Scoring and correction rules

- Treat spoken scores as announcements of the current score, in server-first order, not unconditional requests to add a point.
- Apply an announcement automatically only when it identifies an unambiguous legal progression from the current state. Reannouncing the current score is a no-op.
- Do not globally deduplicate text: the same score can legitimately recur after deuce or in a new game. Duplicate transport/recognition results are deduplicated by identity.
- A score alone can be ambiguous after missed calls, deuce cycles, or a game ending and the next game beginning. Use explicit point-to-team commands or tactile correction; do not invent intervening points or silently close a game from a new zero-zero call.
- A fault does not automatically mean a lost point. Track first/second serve state if fault handling is enabled, and provide undo for accidental calls.
- Preserve stable team IDs while service changes. Configure doubles service order and receiving choices explicitly, including No-Ad and tiebreak cases.
- Validate match formats, serving/receiving transitions, changeovers, and tiebreak rules against the applicable official rules before implementing them. Express rules must be an explicitly defined preset.
- Undo appends a correction event and recomputes dependent score/server/timer state. Never delete already-synced events to hide a correction.
- Disputes pause automatic voice updates and provide an explicit correction flow. Final match-result overrides require deliberate user input in the product.
- Ignore voice scoring during sponsor audio and synthesized announcements in the initial implementation to prevent self-triggering.

## Offline and synchronization contract

- One active scorer owns a match for the MVP. The organizer does not independently award points while a court is disconnected.
- Each accepted action receives a unique event ID, match ID, scorer identity, increasing sequence, and rules version. Store the event and outbox entry atomically before showing it as committed.
- The server durably stores events before acknowledging them. Retried events are idempotent; sequence gaps trigger catch-up rather than silent reordering.
- After reconnect, resend unacknowledged events and fetch missing server state. After a process restart, recover from durable local data without needing the network.
- Disconnected dashboard cards show last-update time and stale status. Do not suggest the dashboard is live when the court cannot reach it.
- Handle server/scorer restarts and acknowledgement loss. An unsupported competing scorer or rules-version conflict stops automatic reconciliation and surfaces a recoverable error.
- For the base MVP, rules are selected at match setup. If live rule pushes are added, queue a versioned proposal and apply it only at a supported boundary after court acknowledgement; offline courts show the update as pending.
- Store timer deadlines and state transitions so reconnects do not restart breaks. Local alerts continue without server contact.
- Preload model and sponsor assets before the demonstration. Test offline cold start as well as losing connection after startup, particularly for SDKs with access-key checks.

## Implementation schedule: 24-hour upper bound

| Time budget | Work | Exit condition |
| --- | --- | --- |
| Hours 0-2 | Confirm hardware, install a minimal scorer, prove voice, choose native or web | One real iPad call produces a result; build/install path works |
| Hours 2-6 | Domain types, tested rules presets, touch scorer, correction | A scripted singles and doubles sequence completes correctly |
| Hours 6-9 | Durable court events, server protocol, organizer with two courts | Touch scoring is visible live; match completion frees a court |
| Hours 9-12 | First provider adapter, command parser, dry-run and guarded voice scoring | Supported calls update the same rules engine as touch |
| Hours 12-15 | Offline restart/reconnect, retries, stale UI, server recovery | Disconnect/restart/reconnect scenario converges without extra points |
| Hours 15-18 | Voice evaluation; second engine only if time permits | Choose one tested demo engine and document limitations |
| Hours 18-21 | Timers, sponsor graphic, accessibility and scoreboard polish | Changeover flow and large-screen scoreboard are demo-ready |
| Hours 21-24 | Device checks, fixes, demo script and buffer | Full demonstration passes on the actual hardware |

If the first two hours do not establish native installation and recognition, stop adding native features and make an explicit platform decision. A laptop-hosted transcription fallback can demonstrate voice, but would lose voice when disconnected from that host; disclose that limitation and retain offline tactile scoring. Do not quietly substitute cloud recognition.

## Verification and delivery

Test meaningful behavior: game/set/match boundaries, advantage and No-Ad, doubles/tiebreak rotations, ambiguous announcements, repeated final results, faults, undo, event replay, retry after lost acknowledgement, reconnect, and offline restart.

Manual iPad checks include microphone denial, model unavailable, changing providers while a result is pending, voice toggling, interruption/background return, long active session, screen legibility, alert audibility, sponsor self-triggering, and the disconnected demonstration.

Deliver the runnable scorer, organizer, sync service, setup instructions, exact tested hardware/provider versions, rules preset definitions, model-test notes, known limitations, and a short demo script. Keep coherent changes in separate descriptive commits, explicitly stage intended files, and exclude build output, credentials, model downloads, and recordings.

## Reference links

These establish available integration paths, not comparative tennis accuracy. Recheck exact APIs and compatibility during the initial spike.

- [FluidAudio](https://github.com/FluidInference/FluidAudio)
- [FluidAudio React Native / Expo wrapper](https://github.com/FluidInference/react-native-fluidaudio)
- [Parakeet v3 model](https://huggingface.co/nvidia/parakeet-tdt-0.6b-v3)
- [Parakeet v3 Core ML conversion](https://huggingface.co/FluidInference/parakeet-tdt-0.6b-v3-coreml)
- [Apple SpeechAnalyzer introduction](https://developer.apple.com/videos/play/wwdc2025/277/)
- [WhisperKit](https://github.com/argmaxinc/whisperkit)
- [Rhino introduction](https://picovoice.ai/docs/rhino/)
- [Rhino web quick start](https://picovoice.ai/docs/quick-start/rhino-web/)
- [Xcode](https://developer.apple.com/documentation/xcode)

## Starting prompt for the next session

> Read the CourtSide MVP plan saved in Vox at docs/courtside-mvp-plan.md. We are building a separate CourtSide AI hackathon project. Begin by confirming the target iPad, available Mac/build setup, and demo languages. Run the time-boxed voice feasibility spike, select one application platform, and implement the plan in small tested commits. Keep voice providers replaceable and preserve offline match scoring. Use the plan's demonstration as the acceptance target.
