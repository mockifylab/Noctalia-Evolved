// Faithful C# port of the vendored fzy fuzzy matcher, third_party/fzy/src/match.c
// (MIT License: Copyright (c) 2014 John Hawthorn; vendored by noctalia-dev).
namespace NoctaliaWin.Core.Fuzzy;

public static class FzyScorer
{
    public const double ScoreMin = double.NegativeInfinity;
    public const double ScoreMax = double.PositiveInfinity;
    public const int MatchMaxLen = 1024;

    private const double ScoreGapLeading = -0.005;
    private const double ScoreGapTrailing = -0.005;
    private const double ScoreGapInner = -0.01;
    private const double ScoreMatchConsecutive = 1.0;
    private const double ScoreMatchSlash = 0.9;
    private const double ScoreMatchWord = 0.8;
    private const double ScoreMatchCapital = 0.7;
    private const double ScoreMatchDot = 0.6;

    public static bool HasMatch(string needle, string haystack)
    {
        int h = 0;
        foreach (char n in needle)
        {
            int found = -1;
            for (int j = h; j < haystack.Length; j++)
            {
                char c = haystack[j];
                if (c == n || char.ToUpperInvariant(c) == char.ToUpperInvariant(n))
                {
                    found = j;
                    break;
                }
            }
            if (found < 0)
                return false;
            h = found + 1;
        }
        return true;
    }

    public static double Score(string needle, string haystack)
    {
        var (ok, positions) = Compute(needle, haystack, wantPositions: false);
        return ok;
    }

    /// Returns (score, matched positions). Positions are null when there is no match
    /// or the candidate is unreasonably large (score = -inf, matching upstream).
    public static (double Score, int[]? Positions) MatchPositions(string needle, string haystack) =>
        Compute(needle, haystack, wantPositions: true);

    private static (double, int[]?) Compute(string needle, string haystack, bool wantPositions)
    {
        if (needle.Length == 0)
            return (ScoreMin, null);

        int n = needle.Length;
        int m = haystack.Length;

        if (m > MatchMaxLen || n > m)
            return (ScoreMin, null);

        if (n == m)
        {
            var all = new int[n];
            for (int i = 0; i < n; i++)
                all[i] = i;
            return (ScoreMax, wantPositions ? all : null);
        }

        var lowerNeedle = new char[n];
        for (int i = 0; i < n; i++)
            lowerNeedle[i] = char.ToLowerInvariant(needle[i]);
        var lowerHaystack = new char[m];
        for (int i = 0; i < m; i++)
            lowerHaystack[i] = char.ToLowerInvariant(haystack[i]);

        // precompute_bonus over the original (non-lowered) haystack.
        var matchBonus = new double[m];
        char lastCh = '/';
        for (int i = 0; i < m; i++)
        {
            matchBonus[i] = ComputeBonus(lastCh, haystack[i]);
            lastCh = haystack[i];
        }

        // Rolling single-row DP, equivalent to the C D[]/M[] row iteration:
        // the C match_row is called with (curr, last) aliasing the same buffers
        // for Score(), so only the final row's M[m-1] is needed there. For
        // positions we need the full matrix.
        double[] lastD, lastM, currD, currM;
        int[]? positions = wantPositions ? new int[n] : null;
        double[][]? fullD = wantPositions ? new double[n][] : null;
        double[][]? fullM = wantPositions ? new double[n][] : null;

        lastD = new double[m];
        lastM = new double[m];
        Array.Fill(lastD, ScoreMin);
        Array.Fill(lastM, ScoreMin);

        for (int i = 0; i < n; i++)
        {
            currD = (double[])lastD.Clone();
            currM = (double[])lastM.Clone();
            MatchRow(i, n, m, lowerNeedle, lowerHaystack, matchBonus, lastD, lastM, currD, currM);
            lastD = currD;
            lastM = currM;
            if (wantPositions)
            {
                fullD![i] = (double[])currD.Clone();
                fullM![i] = (double[])currM.Clone();
            }
        }

        double score = lastM[m - 1];

        if (!wantPositions || positions is null)
            return (score, null);

        if (score <= ScoreMin)
            return (score, null); // upstream leaves the caller's buffer untouched; no match to highlight

        // Backtrace to find the positions of the optimal matching.
        bool matchRequired = false;
        for (int i = n - 1, j = m - 1; i >= 0; i--)
        {
            for (; j >= 0; j--)
            {
                if (fullD![i][j] != ScoreMin &&
                    (matchRequired || fullD![i][j] == fullM![i]![j]))
                {
                    // A consecutive-match path forces the previous character to be a match.
                    matchRequired =
                        i > 0 && j > 0 &&
                        fullM![i]![j] == fullD![i - 1][j - 1] + ScoreMatchConsecutive;
                    positions[i] = j--;
                    break;
                }
            }
        }

        return (score, positions);
    }

    private static void MatchRow(
        int row, int n, int m,
        char[] lowerNeedle, char[] lowerHaystack, double[] matchBonus,
        double[] lastD, double[] lastM,
        double[] currD, double[] currM)
    {
        double prevScore = ScoreMin;
        double gapScore = row == n - 1 ? ScoreGapTrailing : ScoreGapInner;
        double prevD = ScoreMin, prevM = ScoreMin;

        for (int j = 0; j < m; j++)
        {
            if (lowerNeedle[row] == lowerHaystack[j])
            {
                double score = ScoreMin;
                if (row == 0)
                {
                    score = j * ScoreGapLeading + matchBonus[j];
                }
                else if (j > 0)
                {
                    score = Math.Max(
                        prevM + matchBonus[j],
                        // consecutive match, doesn't stack with match_bonus
                        prevD + ScoreMatchConsecutive);
                }
                prevD = lastD[j];
                prevM = lastM[j];
                currD[j] = score;
                currM[j] = prevScore = Math.Max(score, prevScore + gapScore);
            }
            else
            {
                prevD = lastD[j];
                prevM = lastM[j];
                currD[j] = ScoreMin;
                currM[j] = prevScore = prevScore + gapScore;
            }
        }
    }

    private static double ComputeBonus(char lastCh, char ch)
    {
        int state = ch switch
        {
            >= 'A' and <= 'Z' => 2,
            >= 'a' and <= 'z' => 1,
            >= '0' and <= '9' => 1,
            _ => 0,
        };
        return state switch
        {
            0 => 0.0,
            _ => lastCh switch
            {
                '/' => ScoreMatchSlash,
                '-' or '_' or ' ' => ScoreMatchWord,
                '.' => ScoreMatchDot,
                _ when state == 2 && char.IsLower(lastCh) => ScoreMatchCapital,
                _ => 0.0,
            },
        };
    }
}
