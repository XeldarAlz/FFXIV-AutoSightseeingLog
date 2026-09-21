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

        if (!TryRunAfterAction(owningSession))
        {
            ClearRun();
        }
    }

    // The finished run stays on screen while its after-run action runs, and is cleared once that ends.
    private bool TryRunAfterAction(TourSession ending)
    {
        if (ending.AfterActionDispatched)
        {
            return false;
        }

        if (!ending.EndedOnItsOwn)
        {
            Diag("Run ended before it ran out of vistas to work; no after-run action.");
            return false;
        }

        ending.AfterActionDispatched = true;
        var action = Plugin.Instance.Configuration.AfterRun;
        if (action == AfterRunAction.StayLoggedIn)
        {
            Diag("Run ended on its own; the after-run action is StayLoggedIn, nothing to do.");
            return false;
        }

        if (ending.DidNothing)
        {
            Diag($"Run ended on its own without logging a vista; skipping after-run action {action}.");
            return false;
        }

        Diag($"Run ended on its own; starting after-run action {action}.");
        progress.SetPhase(TourPhase.Finishing);
        AutoCommon task = action == AfterRunAction.ReturnToInn ? new AutoReturnToInn() : new AutoAfterRun(action);
        RunTask(task, () =>
        {
            Diag($"After-run action {action} finished.");
            ClearRun();
        });
        return true;
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
