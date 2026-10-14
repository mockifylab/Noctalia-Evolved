// Scoring parity tests against the vendored fzy C reference
// (third_party/fzy/src/match.c + bonus.h + config.h, MIT License,
// Copyright (c) 2014 John Hawthorn). Expected values are hand-derived from
// the C constants: leading gap -0.005, inner gap -0.01, trailing gap -0.005,
// consecutive +1.0, slash +0.9, word +0.8, capital +0.7, dot +0.6.
using NoctaliaWin.Core.Fuzzy;

namespace NoctaliaWin.Tests.Fuzzy;

public class FzyScorerTests
{
    private const double Tol = 1e-9;

    [Theory]
    [InlineData("firefox", "firefox")]
    [InlineData("FF", "ff")]
    public void EqualLengthMatch_ScoresMaximum(string needle, string haystack)
    {
        Assert.True(double.IsPositiveInfinity(FzyScorer.Score(needle, haystack)));
    }

    [Fact]
    public void EqualLengthMatch_PositionsAreEveryIndex()
    {
        var (score, positions) = FzyScorer.MatchPositions("abc", "ABC");

        Assert.True(double.IsPositiveInfinity(score));
        Assert.Equal(new[] { 0, 1, 2 }, positions);
    }

    [Fact]
    public void NeedleLongerThanHaystack_ScoresMinimum()
    {
        Assert.True(double.IsNegativeInfinity(FzyScorer.Score("abcd", "abc")));
    }

    [Fact]
    public void EmptyNeedle_ScoresMinimum()
    {
        Assert.True(double.IsNegativeInfinity(FzyScorer.Score("", "anything")));
    }

    [Fact]
    public void OversizedHaystack_IsUnrankedButStillMatches()
    {
        var longPath = new string('x', FzyScorer.MatchMaxLen + 1);

        Assert.True(FzyScorer.HasMatch("x", longPath));
        Assert.True(double.IsNegativeInfinity(FzyScorer.Score("x", longPath)));
    }

    [Fact]
    public void LeadingGapPenaltyIsAppliedPerCharBeforeTheFirstMatch()
    {
        // "a" at index 0 with the initial '/' bonus: 0.9, minus two trailing gaps.
        Assert.Equal(0.89, FzyScorer.Score("a", "abc"), Tol);
        // "b" at index 1: one leading gap per skipped char, no trailing gap.
        Assert.Equal(-0.005, FzyScorer.Score("b", "ab"), Tol);
    }

    [Fact]
    public void MatchesEarlierInTheStringScoreHigher()
    {
        Assert.True(FzyScorer.Score("b", "bc") > FzyScorer.Score("b", "ab"));
    }

    [Fact]
    public void ConsecutiveMatchesBeatGappedOnes()
    {
        var packed = FzyScorer.Score("ab", "abc");
        var gapped = FzyScorer.Score("ab", "axb");

        Assert.Equal(1.895, packed, Tol);
        Assert.Equal(0.89, gapped, Tol);
        Assert.True(packed > gapped);
    }

    [Fact]
    public void WordBoundaryBonusApplies()
    {
        // 'f' at start (+0.9 slash), 'b' after a space (+0.8 word), two trailing gaps.
        Assert.Equal(1.66, FzyScorer.Score("fb", "foo bar"), Tol);
    }

    [Fact]
    public void CapitalBonusApplies()
    {
        // Camel-haystack: 'B' after lowercase 'o' earns the capital bonus.
        Assert.Equal(1.57, FzyScorer.Score("FB", "FooBar"), Tol);
    }

    [Fact]
    public void DotBonusApplies()
    {
        // 'c' follows '.', which is worth +0.6; two skipped chars cost leading gaps.
        Assert.Equal(0.59, FzyScorer.Score("c", "a.c"), Tol);
    }

    [Fact]
    public void SlashBeatsWordForPathNavigation()
    {
        Assert.True(FzyScorer.Score("d", "/etc/dir") > FzyScorer.Score("d", "/etcadir"));
    }

    [Theory]
    [InlineData("firefox", "Mozilla Firefox", true)]
    [InlineData("FF", "firefox", true)]
    [InlineData("firef", "firefox", true)]
    [InlineData("ofx", "firefox", false)] // not a subsequence in order
    [InlineData("zx", "firefox", false)]
    [InlineData("rfie", "firefox", false)] // subsequence order matters
    public void HasMatchIsCaseInsensitiveSubsequence(string needle, string haystack, bool expected)
    {
        Assert.Equal(expected, FzyScorer.HasMatch(needle, haystack));
    }

    [Fact]
    public void MatchPositionsReturnsOptimalSpan()
    {
        var (_, positions) = FzyScorer.MatchPositions("ab", "axb");

        Assert.Equal(new[] { 0, 2 }, positions);
    }

    [Fact]
    public void MatchPositionsReturnsNullWhenNoMatch()
    {
        var (score, positions) = FzyScorer.MatchPositions("zz", "firefox");

        Assert.True(double.IsNegativeInfinity(score));
        Assert.Null(positions);
    }
}
