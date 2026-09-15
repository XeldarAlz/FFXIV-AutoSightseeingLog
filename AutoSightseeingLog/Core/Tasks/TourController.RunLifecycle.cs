using AutoSightseeingLog.Core.Stats;

namespace AutoSightseeingLog.Core.Tasks;

internal sealed partial class TourController
{
    private void OnTourEnded(TourSession owningSession)
    {
        FinalizeRun(owningSession, sample: true);
        if (!ReferenceEquals(session, owningSession))
        {
            Diag("A run that is no longer live ended; it was recorded and the current run is left alone.");
            return;
        }

        ClearRun();
    }

    // Idempotent through Recorded, so an explicit Stop and a finished task can both call it.
    private void FinalizeRun(TourSession? ending, bool sample)
    {
        if (ending is null || ending.Recorded)
        {
            return;
        }

        ending.Recorded = true;
        ending.EndPause();
        try
        {
            if (sample)
            {
                ending.Sample();
            }

            if (ending.DidNothing)
            {
                Diag("Run logged no vista; nothing recorded to history.");
                return;
            }

            var record = new RunRecord
            {
                StartedAtUtc = ending.StartedAt,
                EndedAtUtc = DateTime.UtcNow,
                DurationSeconds = ending.Elapsed.TotalSeconds,
                VistasLogged = ending.VistasLogged,
                ZonesVisited = ending.ZonesVisited,
                VistaNames = [.. ending.VistaNames],
            };
            Plugin.Instance.History.Append(record);
            Diag($"Run recorded to history: {record.VistasLogged} vistas in {record.ZonesVisited} zones over {record.Duration}.");
        }
        catch (Exception exception)
        {
            Diag($"FinalizeRun failed to record history: {exception.Message}");
        }
    }
}
