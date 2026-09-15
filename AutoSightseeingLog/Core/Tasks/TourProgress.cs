namespace AutoSightseeingLog.Core.Tasks;

// Holds the plan in the order the run works it; the windows read each vista's state live from the log.
internal sealed class TourProgress
{
    private ushort[] queue = [];

    public TourPhase Phase { get; private set; } = TourPhase.Idle;

    public ReadOnlySpan<ushort> Queue => queue;

    public void SetPhase(TourPhase phase) => Phase = phase;

    public void SetQueue(ushort[] ordered) => queue = ordered;

    public void Reset()
    {
        Phase = TourPhase.Idle;
        queue = [];
    }
}
