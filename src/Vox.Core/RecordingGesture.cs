namespace Vox.Core;

public enum RecordingMode { Idle, Holding, AwaitingSecondTap, Locked, Processing }
public enum RecordingAction { None, Begin, Lock, Finish, Cancel }

/// <summary>UI-independent gestures. Call Tick only while waiting for a second tap.</summary>
public sealed class RecordingGesture
{
    public const int TapDurationMs = 180;
    public const int DoubleTapWindowMs = 350;
    public RecordingMode Mode { get; private set; }
    public bool IsKeyDown { get; private set; }
    public long FinishDeadline { get; private set; }
    private long _downAt;

    public RecordingAction KeyDown(long now)
    {
        if (IsKeyDown) return RecordingAction.None;
        IsKeyDown = true;
        switch (Mode)
        {
            case RecordingMode.Idle:
                _downAt = now;
                Mode = RecordingMode.Holding;
                return RecordingAction.Begin;
            case RecordingMode.AwaitingSecondTap:
                if (now > FinishDeadline) return Finish();
                Mode = RecordingMode.Locked;
                return RecordingAction.Lock;
            case RecordingMode.Locked:
                return Finish();
            default:
                return RecordingAction.None;
        }
    }

    public RecordingAction KeyUp(long now)
    {
        if (!IsKeyDown) return RecordingAction.None;
        IsKeyDown = false;
        if (Mode != RecordingMode.Holding) return RecordingAction.None;
        if (now - _downAt > TapDurationMs) return Finish();
        FinishDeadline = now + DoubleTapWindowMs;
        Mode = RecordingMode.AwaitingSecondTap;
        return RecordingAction.None;
    }

    public RecordingAction Tick(long now) =>
        Mode == RecordingMode.AwaitingSecondTap && now >= FinishDeadline
            ? Finish() : RecordingAction.None;

    public RecordingAction Cancel()
    {
        if (Mode == RecordingMode.Idle) return RecordingAction.None;
        Mode = RecordingMode.Idle;
        return RecordingAction.Cancel;
    }

    public void Complete() => Mode = RecordingMode.Idle;

    private RecordingAction Finish()
    {
        Mode = RecordingMode.Processing;
        return RecordingAction.Finish;
    }
}
