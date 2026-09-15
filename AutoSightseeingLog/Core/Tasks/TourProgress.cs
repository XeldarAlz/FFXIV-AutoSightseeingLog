namespace AutoSightseeingLog.Core.Tasks;

// Holds the plan in the order the run works it; the windows read each vista's state live from the log.
internal sealed class TourProgress
{
    private ushort[] queue = [];

    public TourPhase Phase { get; private set; } = TourPhase.Idle;

    public ReadOnlySpan<ushort> Queue => queue;

    // Log number of the vista the run is working on, or 0 between vistas.
    public ushort CurrentVista { get; private set; }

    public void SetPhase(TourPhase phase) => Phase = phase;

    public void SetQueue(ushort[] ordered) => queue = ordered;

    public void SetVista(ushort number) => CurrentVista = number;

    public void ClearVista() => CurrentVista = 0;

    public void Reset()
    {
        Phase = TourPhase.Idle;
        queue = [];
        CurrentVista = 0;
    }
}
