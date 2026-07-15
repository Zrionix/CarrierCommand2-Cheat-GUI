using System.Globalization;

namespace CC2CheatGUI.Core.Ram;

/// <summary>
/// An array-of-bytes pattern with wildcards, e.g. "FF 8E D0 00 00 00 8B 8E ?? ?? ?? ??".
/// <c>??</c> (or <c>?</c>) is a wildcard byte.
/// </summary>
public sealed class Signature
{
    /// <summary>Each element is a concrete byte, or null for a wildcard.</summary>
    public byte?[] Pattern { get; }
    public int Length => Pattern.Length;
    public string Text { get; }

    public Signature(string pattern)
    {
        Text = pattern.Trim();
        var tokens = Text.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        Pattern = new byte?[tokens.Length];
        for (int i = 0; i < tokens.Length; i++)
        {
            var t = tokens[i];
            if (t == "??" || t == "?")
                Pattern[i] = null;
            else
                Pattern[i] = byte.Parse(t, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }
    }

    /// <summary>Index of the first match of the pattern in <paramref name="hay"/>, or -1.</summary>
    public int IndexOf(ReadOnlySpan<byte> hay, int start = 0)
    {
        var pat = Pattern;
        int last = hay.Length - pat.Length;
        for (int i = start; i <= last; i++)
        {
            bool ok = true;
            for (int j = 0; j < pat.Length; j++)
            {
                if (pat[j] is byte b && hay[i + j] != b) { ok = false; break; }
            }
            if (ok) return i;
        }
        return -1;
    }

    /// <summary>All match offsets of the pattern in <paramref name="hay"/>.</summary>
    public IEnumerable<int> AllIndexes(byte[] hay)
    {
        int start = 0;
        while (true)
        {
            int idx = IndexOf(hay, start);
            if (idx < 0) yield break;
            yield return idx;
            start = idx + 1;
        }
    }
}
