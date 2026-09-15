using AutoSightseeingLog.Core.External;
using clib.Services;

namespace AutoSightseeingLog.Core.Tasks;

internal sealed partial class TourController
{
    private const int SessionSampleIntervalMs = 1_000;

    private readonly TourProgress progress = new();

    private TourSession? session;
    private AutoCommon? currentTask;
    private long nextSessionSampleAtMs;

    public bool Running => Svc.Automation.Running || Paused;

    public string Status => PauseReason switch
    {
        PauseReason.InContent => "Paused while you are in content",
        PauseReason.Manual    => "Paused",
        _                     => Svc.Automation.CurrentTask?.Status ?? "Idle",
    };

    public TourPhase Phase => progress.Phase;

    public TourProgress Progress => progress;

    public TourSession? SessionSnapshot => session;

    private static void Diag(string message)
        => ECommons.DalamudServices.Svc.Log.Info($"{AslConstants.LogPrefix} {message}");

    public void Start(ushort[] plan)
    {
        if (plan.Length == 0)
        {
            Diag("Start aborted: no vista in the plan can be worked.");
            return;
        }

        if (!RequiredPluginsReady())
        {
            return;
        }

        PauseReason = PauseReason.None;
        session = new TourSession(plan);
        Diag($"Run starting: {plan.Length} vista(s).");
        StartTour(session);
    }

    public void Stop()
    {
        var ending = session;
        var wasPaused = Paused;
        currentTask = null;
        PauseReason = PauseReason.None;
        Svc.Automation.Stop();

        // Pause already credited the run, and anything done since was the player's own play.
        FinalizeRun(ending, sample: !wasPaused);
        ClearRun();
        if (ending is not null)
        {
            Diag("Stop requested; session cleared.");
        }
    }

    // Credits vistas as they are recorded, so the stat tiles keep pace with the log. A paused run is left alone,
    // because Resume makes the log its new zero point.
    public void Tick()
    {
        if (session is null || session.Recorded || Paused)
        {
            return;
        }

        var now = Environment.TickCount64;
        if (now < nextSessionSampleAtMs)
        {
            return;
        }

        nextSessionSampleAtMs = now + SessionSampleIntervalMs;
        session.Sample();
    }

    private static bool RequiredPluginsReady()
    {
        if (ExternalPlugins.AllRequiredInstalled())
        {
            return true;
        }

        var missing = ExternalPlugins.MissingRequiredNames();
        Diag($"Start aborted: required plugins missing ({missing}).");
        ECommons.DalamudServices.Svc.Chat.PrintError($"{AslConstants.LogPrefix} Cannot start: install all required plugins first ({missing}).");
        return false;
    }

    private void StartTour(TourSession owningSession)
    {
        progress.Reset();
        progress.SetPhase(TourPhase.Reading);
        RunTask(new AutoTour(owningSession, progress), () => OnTourEnded(owningSession));
    }

    private void RunTask(AutoCommon task, Action onCompleted)
    {
        currentTask = task;
        Svc.Automation.Start(task, OnCompleted: () =>
        {
            if (!ReferenceEquals(currentTask, task))
            {
                Diag($"{task.GetType().Name} finished but is no longer the current task (stopped, paused, or superseded); skipping hand-off.");
                return;
            }

            currentTask = null;
            onCompleted();
        });
    }

    private void ClearRun()
    {
        session = null;
        progress.Reset();
    }
}

internal enum TourPhase { Idle, Reading, Paused }
