using Lumina.Text.ReadOnly;

namespace AutoSightseeingLog.Core;

internal static class GameText
{
    // German names carry soft hyphens that only the game's own line breaking uses.
    private const string SoftHyphen = "­";

    public static string Plain(ReadOnlySeString text)
    {
        var extracted = text.ExtractText();
        if (!extracted.Contains(SoftHyphen, StringComparison.Ordinal))
        {
            return extracted;
        }

        return extracted.Replace(SoftHyphen, string.Empty, StringComparison.Ordinal);
    }
}
