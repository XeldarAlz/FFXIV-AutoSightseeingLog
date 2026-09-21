using AutoSightseeingLog.Core.Vistas;

namespace AutoSightseeingLog.Core.Tasks;

internal sealed class TourSession
{
    private readonly ushort[] plan;
    private readonly bool[] credited;
    private readonly bool[] baseline;
    private readonly HashSet<uint> zones = new();

    private long pausedMs;
    private long pauseStartedAtMs;

    public TourSession(ushort[] plan)
    {
        this.plan = plan;
        credited = new bool[plan.Length];
        baseline = new bool[plan.Length];
        Rebaseline();
    }

    public DateTime StartedAt { get; } = DateTime.UtcNow;

    public int VistasLogged { get; private set; }

    public int ZonesVisited => zones.Count;

    public List<string> VistaNames { get; } = [];

    public IReadOnlyList<ushort> Plan => plan;

    public bool Recorded { get; set; }

    // Set when the tour ran out of vistas it can work; a Stop, a logout or a fault never sets it.
    public bool EndedOnItsOwn { get; set; }

    public bool AfterActionDispatched { get; set; }

    public bool DidNothing => VistasLogged == 0;

    public TimeSpan Elapsed => DateTime.UtcNow - StartedAt - TimeSpan.FromMilliseconds(PausedTotalMs);

    private long PausedTotalMs => pausedMs + (pauseStartedAtMs == 0 ? 0 : Environment.TickCount64 - pauseStartedAtMs);

    // Only a vista recorded after the run started, or after it last resumed, is the run's own.
    public void Sample()
    {
        VistaLog.Refresh();
        if (!VistaLog.Loaded)
        {
            return;
        }

        for (var slot = 0; slot < plan.Length; slot++)
        {
            if (credited[slot] || baseline[slot] || !VistaLog.IsRecorded(plan[slot]))
            {
                continue;
            }

            credited[slot] = true;
            VistasLogged++;
            VistaNames.Add(VistaRegistry.Name(plan[slot]));
            if (VistaRegistry.TryGet(plan[slot], out var vista))
            {
                zones.Add(vista.TerritoryId);
            }
        }
    }

    public void Rebaseline()
    {
        VistaLog.Refresh(force: true);
        for (var slot = 0; slot < plan.Length; slot++)
        {
            baseline[slot] = !credited[slot] && VistaLog.IsRecorded(plan[slot]);
        }
    }

    public void BeginPause()
    {
        if (pauseStartedAtMs != 0)
        {
            return;
        }

        pauseStartedAtMs = Environment.TickCount64;
    }

    public void EndPause()
    {
        if (pauseStartedAtMs == 0)
        {
            return;
        }

        pausedMs += Environment.TickCount64 - pauseStartedAtMs;
        pauseStartedAtMs = 0;
    }
}
