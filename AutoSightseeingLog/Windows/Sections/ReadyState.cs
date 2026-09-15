using AutoSightseeingLog.Core.External;
using AutoSightseeingLog.Core.Localization;
using AutoSightseeingLog.Core.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using System.Numerics;

namespace AutoSightseeingLog.Windows.Sections;

internal static class ReadyState
{
    public enum Kind { SetupNeeded, Offline, PickVistas, Locked, AllDone, Ready, Running, Paused }

    public readonly record struct Info(Kind Kind, Vector4 Accent, Vector4 AccentSoft, FontAwesomeIcon Icon, string Title, string Detail);

    private static int cachedFrame = -1;
    private static Info cached;

    public static Info Resolve(Configuration configuration, TourController controller)
    {
        var frame = ImGui.GetFrameCount();
        if (frame == cachedFrame)
        {
            return cached;
        }

        cached = Compute(configuration, controller);
        cachedFrame = frame;
        return cached;
    }

    private static Info Compute(Configuration configuration, TourController controller)
    {
        if (controller.Running)
        {
            if (controller.Paused)
            {
                var detail = controller.PauseReason == PauseReason.InContent
                    ? Loc.T(L.Vistas.DetailPausedInContent)
                    : Loc.T(L.Vistas.DetailPausedManual);
                return new Info(Kind.Paused, Styling.AccentAmber, Styling.AccentAmberSoft, FontAwesomeIcon.Pause, Loc.T(L.Vistas.TitlePaused), detail);
            }

            return new Info(Kind.Running, Styling.AccentBlue, Styling.AccentBlueSoft, FontAwesomeIcon.Binoculars, Loc.T(L.Vistas.TitleRunning),
                PhaseLabel(controller.Phase));
        }

        if (!ExternalPlugins.AllRequiredInstalled())
        {
            return new Info(Kind.SetupNeeded, Styling.AccentRose, Styling.AccentRoseSoft, FontAwesomeIcon.ExclamationTriangle,
                Loc.T(L.Vistas.TitleSetupNeeded), Loc.T(L.Vistas.DetailSetupNeeded));
        }

        return TourLauncher.Assess(configuration).Readiness switch
        {
            TourLauncher.Readiness.Offline => new Info(Kind.Offline, Styling.TextDim, Styling.TextSecondary, FontAwesomeIcon.UserClock,
                Loc.T(L.Vistas.TitleOffline), Loc.T(L.Vistas.DetailOffline)),
            TourLauncher.Readiness.NothingPicked => new Info(Kind.PickVistas, Styling.AccentAmber, Styling.AccentAmberSoft, FontAwesomeIcon.MapMarkedAlt,
                Loc.T(L.Vistas.TitlePick), Loc.T(L.Vistas.DetailPick)),
            TourLauncher.Readiness.Locked => new Info(Kind.Locked, Styling.AccentAmber, Styling.AccentAmberSoft, FontAwesomeIcon.Lock,
                Loc.T(L.Vistas.TitleLocked), Loc.T(L.Vistas.DetailLocked)),
            TourLauncher.Readiness.AllDone => new Info(Kind.AllDone, Styling.AccentMint, Styling.AccentMintSoft, FontAwesomeIcon.CheckDouble,
                Loc.T(L.Vistas.TitleAllDone), Loc.T(L.Vistas.DetailAllDone)),
            _ => new Info(Kind.Ready, Styling.AccentMint, Styling.AccentMintSoft, FontAwesomeIcon.CheckCircle,
                Loc.T(L.Vistas.TitleReady), Loc.T(L.Vistas.DetailReady)),
        };
    }

    public static string ShortLabel(Kind kind) => kind switch
    {
        Kind.Running     => Loc.T(L.Shell.StatusRunning),
        Kind.Paused      => Loc.T(L.Shell.StatusPaused),
        Kind.Ready       => Loc.T(L.Shell.StatusReady),
        Kind.PickVistas  => Loc.T(L.Shell.StatusPickVistas),
        Kind.Locked      => Loc.T(L.Shell.StatusLocked),
        Kind.Offline     => Loc.T(L.Shell.StatusOffline),
        Kind.AllDone     => Loc.T(L.Shell.StatusAllDone),
        Kind.SetupNeeded => Loc.T(L.Shell.StatusSetupNeeded),
        _                => Loc.T(L.Shell.StatusIdle),
    };

    public static string PhaseLabel(TourPhase phase) => phase switch
    {
        TourPhase.Reading    => Loc.T(L.Run.PhaseReading),
        TourPhase.Travelling => Loc.T(L.Run.PhaseTravelling),
        TourPhase.Waiting    => Loc.T(L.Run.PhaseWaiting),
        TourPhase.Emoting    => Loc.T(L.Run.PhaseEmoting),
        TourPhase.Paused     => Loc.T(L.Run.PhasePaused),
        _                    => Loc.T(L.Run.PhaseStandingBy),
    };
}
