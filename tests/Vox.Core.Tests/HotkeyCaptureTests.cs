using Vox.Core;
using Xunit;

namespace Vox.Core.Tests;

public sealed class HotkeyCaptureTests
{
    [Fact]
    public void CapturesAltF13FromRawKeyEdges()
    {
        var capture = new HotkeyCapture();

        Assert.Null(capture.Process(0xA4, down: true));
        var result = capture.Process(0x7C, down: true);

        Assert.Equal(new HotkeyCaptureResult(0x7C, HotkeyModifiers.Alt), result);
    }

    [Fact]
    public void CapturesAReleasedModifierAsThePrimaryKey()
    {
        var capture = new HotkeyCapture();

        Assert.Null(capture.Process(0xA3, down: true));
        Assert.Equal(new HotkeyCaptureResult(0xA3, HotkeyModifiers.None), capture.Process(0xA3, down: false));
    }

    [Fact]
    public void EscapeCancelsCapture()
    {
        var result = new HotkeyCapture().Process(0x1B, down: true);

        Assert.True(result?.Cancelled);
    }
}
