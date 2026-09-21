using clib.Services;

namespace AutoSightseeingLog.Core.Tasks;

internal enum PauseReason { None, Manual, InContent }

internal sealed partial class TourController
{
    public PauseReason PauseReason { get; private set; } = PauseReason.None;

    public bool Paused => PauseReason != PauseReason.None;

    public bool CanPause => session is not null && Phase is not (TourPhase.Idle or TourPhase.Paused or TourPhase.Finishing);

    public void Pause(PauseReason reason)
    {
        if (reason == PauseReason.None)
        {
            return;
        }

        if (Paused)
        {
            if (reason == PauseReason.Manual && PauseReason == PauseReason.InContent)
            {
                PauseReason = PauseReason.Manual;
                Diag("Auto-pause promoted to a manual pause; leaving content will no longer resume.");
            }

            return;
        }

        if (!CanPause)
        {
            if (reason == PauseReason.Manual)
            {
                ECommons.DalamudServices.Svc.Chat.Print($"{AslConstants.LogPrefix} Nothing to pause.");
            }

            return;
        }

        var pausing = session!;
        PauseReason = reason;
        progress.SetPhase(TourPhase.Paused);
        // Resume re-baselines the session, so progress up to this moment has to be credited now.
        pausing.Sample();
        pausing.BeginPause();
        currentTask = null;
        Svc.Automation.Stop();
        ReleaseHelpers();

        Diag($"Run paused ({reason}); session kept at {pausing.VistasLogged} vistas.");
        ECommons.DalamudServices.Svc.Chat.Print(reason == PauseReason.InContent
            ? $"{AslConstants.LogPrefix} Paused: you are in instanced content. The tour resumes once you are back outside."
            : $"{AslConstants.LogPrefix} Paused. Your plan and session stats are kept until you resume or stop.");
    }

    public void Resume()
    {
        if (!Paused)
        {
            return;
        }

        var resuming = session;
        if (resuming is null)
        {
            Diag("Resume requested with no session; stopping instead.");
            Stop();
            return;
        }

        PauseReason = PauseReason.None;
        resuming.EndPause();
        resuming.Rebaseline();
        Diag("Resuming the tour.");
        ECommons.DalamudServices.Svc.Chat.Print($"{AslConstants.LogPrefix} Resuming the tour.");
        StartTour(resuming);
    }

    public void TogglePause()
    {
        if (Paused)
        {
            Resume();
            return;
        }

        Pause(PauseReason.Manual);
    }
}
