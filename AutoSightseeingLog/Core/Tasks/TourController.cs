using AutoSightseeingLog.Core.External;
using AutoSightseeingLog.Core.Ipc;
using AutoSightseeingLog.Core.Travel;
using AutoSightseeingLog.Core.Vistas;
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

        if (!RequiredPluginsReady() || !SightseeingLogReady())
        {
            return;
        }

        NavmeshSettings.WarnIfInputCancelsMovement();
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
        ReleaseHelpers();

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

    // The log records nothing before its quest. A run can hand that quest to the questing plugin, but not without it.
    private static bool SightseeingLogReady()
    {
        VistaLog.Refresh();
        if (VistaLog.LogUnlocked || ExternalPlugins.IsInstalled(ExternalPlugin.Questionable))
        {
            return true;
        }

        var questName = GameNames.Quest(VistaData.UnlockQuestId);
        var zone = TerritoryNames.Of(VistaData.UnlockQuestTerritoryId);
        Diag("Start aborted: the Sightseeing Log is locked and Questionable is not installed.");
        ECommons.DalamudServices.Svc.Chat.PrintError($"{AslConstants.LogPrefix} Cannot start: your Sightseeing Log is locked. Complete “{questName}” in {zone}, or install Questionable and the run does it for you.");
        return false;
    }

    private void StartTour(TourSession owningSession)
    {
        progress.Reset();
        progress.SetPhase(TourPhase.Reading);
        owningSession.EndedOnItsOwn = false;
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

    // A stopped task unwinds on a later frame, and after an unload that frame may never come, so the pathfinder and the
    // combat plugin's movement are released here as well.
    private static void ReleaseHelpers()
    {
        NavmeshIPC.Instance.Stop();
        BossModIPC.Instance.ReleaseMovement();
    }
}

internal enum TourPhase { Idle, Reading, Unlocking, Travelling, Waiting, Emoting, Finishing, Paused }
