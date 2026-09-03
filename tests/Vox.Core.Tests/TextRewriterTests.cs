using Vox.Core;
using Xunit;

namespace Vox.Core.Tests;

public sealed class TextRewriterTests
{
    [Fact]
    public void MatchesWholeWordsAndPreservesSurroundingPunctuation()
    {
        var rewriter = new TextRewriter([new("vox", "Vox")]);
        Assert.Equal("Vox, VOXEL and Vox!", rewriter.Apply("vox, VOXEL and VOX!"));
    }

    [Fact]
    public void LongerPhrasesWinRegardlessOfRuleOrder()
    {
        var rewriter = new TextRewriter([new("open", "launch"), new("open ai", "OpenAI")]);
        Assert.Equal("OpenAI is launch.", rewriter.Apply("OPEN AI is open."));
    }

    [Fact]
    public void PhraseWhitespaceCanVary()
    {
        var rewriter = new TextRewriter([new("open ai", "OpenAI")]);
        Assert.Equal("OpenAI", rewriter.Apply("Open  \nAI"));
    }

    [Fact]
    public void ReplacementOutputDoesNotCascade()
    {
        var rewriter = new TextRewriter([new("alpha", "beta"), new("beta", "gamma")]);
        Assert.Equal("beta gamma", rewriter.Apply("alpha beta"));
    }

    [Fact]
    public void BothSidesAreLiteralText()
    {
        var rewriter = new TextRewriter([new("c++", "$1 C++")]);
        Assert.Equal("Use $1 C++, not ccc.", rewriter.Apply("Use C++, not ccc."));
    }

    [Fact]
    public void EmptyReplacementRemovesMatchedText()
    {
        Assert.Equal("", new TextRewriter([new("delete this", "")]).Apply("delete this"));
    }

    [Fact]
    public void DuplicatePhrasesAreRejectedAfterNormalizingCaseAndSpaces()
    {
        Assert.Throws<ArgumentException>(() => new TextRewriter([new("open ai", "one"), new(" OPEN  AI ", "two")]));
    }

    [Fact]
    public void BlankSourcesAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new TextRewriter([new("  ", "text")]));
    }

    [Fact]
    public void NoRulesLeaveTranscriptUnchanged()
    {
        Assert.Equal("Hello, world!", new TextRewriter([]).Apply("Hello, world!"));
    }
}
