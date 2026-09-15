namespace AutoSightseeingLog.Core.Vistas;

// Earth unix seconds, End exclusive. A vista with no time or weather rule is open for good.
internal readonly record struct VistaWindow(long Start, long End)
{
    public static readonly VistaWindow Always = new(long.MinValue, long.MaxValue);

    public bool IsEndless => End == long.MaxValue;

    public bool IsOpenAt(long unixSeconds) => Start <= unixSeconds && unixSeconds < End;
}
