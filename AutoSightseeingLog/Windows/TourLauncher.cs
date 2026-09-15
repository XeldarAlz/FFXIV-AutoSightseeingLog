using AutoSightseeingLog.Core.Localization;
using AutoSightseeingLog.Core.Time;
using AutoSightseeingLog.Core.Vistas;
using Dalamud.Bindings.ImGui;

namespace AutoSightseeingLog.Windows;

internal static class TourLauncher
{
    public enum Readiness : byte
    {
        Offline,
        NothingPicked,
        AllDone,
        Locked,
        Ready,
    }

    public readonly record struct Plan(Readiness Readiness, VistaWorkload Workload);

    private const int CountShift = 16;
    private const long CountMask = 0xFFFF;

    private static int cachedFrame = -1;
    private static Plan cachedPlan;
    private static CachedText subjectText;
    private static CachedText sublabelText;

    public static void Start()
    {
        var plugin = Plugin.Instance;
        plugin.Controller.Start(VistaSelection.ResolvePlan(plugin.Configuration.SelectedVistas, EorzeaTime.Now()));
    }

    public static Plan Assess(Configuration configuration)
    {
        var frame = ImGui.GetFrameCount();
        if (frame == cachedFrame)
        {
            return cachedPlan;
        }

        cachedPlan = Compute(configuration);
        cachedFrame = frame;
        return cachedPlan;
    }

    // What the plan sentence names: every picked vista not yet in the log, locked ones included.
    public static string Subject(Configuration configuration, in Plan plan)
    {
        var count = plan.Readiness == Readiness.Offline
            ? configuration.SelectedVistas.Count
            : plan.Workload.Selected - plan.Workload.Recorded;
        return count <= 0
            ? Loc.T(L.Plan.VistasNone)
            : subjectText.Get(count, static key => Loc.Plural(L.Plan.VistasCount, (int)key));
    }

    public static string Sublabel(Configuration configuration, in Plan plan)
    {
        if (plan.Readiness != Readiness.Ready)
        {
            return Subject(configuration, plan);
        }

        var key = (long)plan.Workload.Pending << CountShift | (plan.Workload.Open & CountMask);
        return sublabelText.Get(key, static key => Loc.T(L.Tour.StartSub,
            Loc.Plural(L.Plan.VistasCount, (int)(key >> CountShift)),
            Loc.Plural(L.Tour.OpenNow, (int)(key & CountMask))));
    }

    public static string Reason(in Plan plan) => plan.Readiness switch
    {
        Readiness.Ready         => string.Empty,
        Readiness.Offline       => Loc.T(L.Tour.ReasonOffline),
        Readiness.NothingPicked => Loc.T(L.Tour.ReasonPick),
        Readiness.Locked        => Loc.T(L.Tour.ReasonLocked),
        _                       => Loc.T(L.Tour.ReasonAllDone),
    };

    private static Plan Compute(Configuration configuration)
    {
        VistaLog.Refresh();
        if (!VistaLog.Loaded)
        {
            return new Plan(Readiness.Offline, default);
        }

        var workload = VistaSelection.Measure(configuration.SelectedVistas, EorzeaTime.Now());
        var readiness = workload.Selected == 0 ? Readiness.NothingPicked
            : workload.Pending > 0 ? Readiness.Ready
            : workload.Locked > 0 ? Readiness.Locked
            : Readiness.AllDone;
        return new Plan(readiness, workload);
    }
}
