using Vox.Core;
using Xunit;

namespace Vox.Core.Tests;

public class RecordingGestureTests
{
    [Fact]
    public void HoldFinishesImmediatelyOnRelease()
    {
        var gesture = new RecordingGesture();
        Assert.Equal(RecordingAction.Begin, gesture.KeyDown(0));
        Assert.Equal(RecordingAction.Finish, gesture.KeyUp(1000));
        Assert.Equal(RecordingMode.Processing, gesture.Mode);
        Assert.Equal(RecordingAction.None, gesture.Tick(2000));
    }

    [Fact]
    public void DoubleTapLocksWithoutSubmittingFirstTap()
    {
        var gesture = new RecordingGesture();
        gesture.KeyDown(0);
        Assert.Equal(RecordingAction.None, gesture.KeyUp(50));
        Assert.Equal(RecordingAction.None, gesture.Tick(150));
        Assert.Equal(RecordingAction.Lock, gesture.KeyDown(200));
        Assert.Equal(RecordingAction.None, gesture.KeyUp(250));
        Assert.Equal(RecordingAction.None, gesture.Tick(5000));
        Assert.Equal(RecordingMode.Locked, gesture.Mode);
        Assert.Equal(RecordingAction.Finish, gesture.KeyDown(6000));
        Assert.Equal(RecordingAction.None, gesture.KeyUp(6050));
    }

    [Fact]
    public void SingleTapEventuallyFinishesExactlyOnce()
    {
        var gesture = new RecordingGesture();
        gesture.KeyDown(0);
        gesture.KeyUp(50);
        Assert.Equal(RecordingAction.None, gesture.Tick(399));
        Assert.Equal(RecordingAction.Finish, gesture.Tick(400));
        Assert.Equal(RecordingAction.None, gesture.Tick(401));
    }

    [Fact]
    public void AutoRepeatDoesNotStartOrStopRecording()
    {
        var gesture = new RecordingGesture();
        gesture.KeyDown(0);
        for (var i = 1; i < 100; i++) Assert.Equal(RecordingAction.None, gesture.KeyDown(i));
        Assert.Equal(RecordingAction.Finish, gesture.KeyUp(1000));
    }

    [Fact]
    public void EscapeDuringDoubleTapWaitNeverSubmits()
    {
        var gesture = new RecordingGesture();
        gesture.KeyDown(0);
        gesture.KeyUp(50);
        Assert.Equal(RecordingAction.Cancel, gesture.Cancel());
        Assert.Equal(RecordingAction.None, gesture.Tick(500));
        Assert.Equal(RecordingAction.Begin, gesture.KeyDown(600));
    }

    [Fact]
    public void CancelWhileHeldDoesNotRestartOnRepeat()
    {
        var gesture = new RecordingGesture();
        gesture.KeyDown(0);
        gesture.Cancel();
        Assert.Equal(RecordingAction.None, gesture.KeyDown(100));
        Assert.Equal(RecordingAction.None, gesture.KeyUp(200));
        Assert.Equal(RecordingAction.Begin, gesture.KeyDown(300));
    }

    [Fact]
    public void LateSecondTapCannotLockExpiredSession()
    {
        var gesture = new RecordingGesture();
        gesture.KeyDown(0);
        gesture.KeyUp(50);
        Assert.Equal(RecordingAction.Finish, gesture.KeyDown(401));
        Assert.Equal(RecordingMode.Processing, gesture.Mode);
    }

    [Fact]
    public void BusyKeypressCannotStartAnotherTranscription()
    {
        var gesture = new RecordingGesture();
        gesture.KeyDown(0);
        gesture.KeyUp(1000);
        Assert.Equal(RecordingAction.None, gesture.KeyDown(1100));
        gesture.Complete();
        Assert.Equal(RecordingAction.None, gesture.KeyDown(1200));
        gesture.KeyUp(1300);
        Assert.Equal(RecordingAction.Begin, gesture.KeyDown(1400));
    }
}
