namespace AutoSightseeingLog.Core.Vistas;

internal static class VistaPlan
{
    private enum Urgency : byte
    {
        Open,
        Waiting,
        NoWindow,
    }

    private readonly record struct Entry(Urgency Urgency, long Time, ushort Number);

    private static readonly Comparison<Entry> byUrgency = static (left, right) =>
    {
        var urgency = left.Urgency.CompareTo(right.Urgency);
        if (urgency != 0)
        {
            return urgency;
        }

        var time = left.Time.CompareTo(right.Time);
        return time != 0 ? time : left.Number.CompareTo(right.Number);
    };

    // Open vistas lead, the soonest to close first; then those waiting for a window, the soonest to open first.
    public static ushort[] Order(IReadOnlyList<ushort> plan, long now)
    {
        var entries = new Entry[plan.Count];
        for (var index = 0; index < plan.Count; index++)
        {
            var number = plan[index];
            if (!VistaRegistry.TryGet(number, out var vista) || !VistaLog.TryGetWindow(vista, now, out var window))
            {
                entries[index] = new Entry(Urgency.NoWindow, long.MaxValue, number);
                continue;
            }

            entries[index] = window.IsOpenAt(now)
                ? new Entry(Urgency.Open, window.End, number)
                : new Entry(Urgency.Waiting, window.Start, number);
        }

        Array.Sort(entries, byUrgency);
        var ordered = new ushort[entries.Length];
        for (var index = 0; index < entries.Length; index++)
        {
            ordered[index] = entries[index].Number;
        }

        return ordered;
    }
}
