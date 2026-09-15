using AutoSightseeingLog.Core.Time;
using AutoSightseeingLog.Core.Travel;
using AutoSightseeingLog.Core.Vistas;
using ECommons.DalamudServices;
using System.Threading.Tasks;

namespace AutoSightseeingLog.Core.Tasks;

// v0.1 scaffold: reads the plan and reports it. Travel, waiting for a window and the emote land in later phases.
internal sealed class AutoTour(TourSession session, TourProgress progress) : AutoCommon
{
    private const int ReadSettleMs = 1_500;

    protected override async Task Execute()
    {
        Status = "Reading your Sightseeing Log…";
        VistaLog.Refresh(force: true);
        await DelayMs(ReadSettleMs);

        var now = EorzeaTime.Now();
        var ordered = VistaPlan.Order(session.Plan, now);
        progress.SetQueue(ordered);

        var open = 0;
        var waiting = 0;
        var zones = new HashSet<uint>();
        for (var index = 0; index < ordered.Length; index++)
        {
            if (!VistaRegistry.TryGet(ordered[index], out var vista))
            {
                continue;
            }

            var status = VistaLog.Status(vista, now);
            if (status == VistaStatus.Open)
            {
                open++;
            }
            else if (status == VistaStatus.Waiting)
            {
                waiting++;
            }

            zones.Add(vista.TerritoryId);
            Diag($"Plan: #{vista.Number:000} {VistaRegistry.Name(vista.Number)} in {TerritoryNames.Of(vista.TerritoryId)}: {status}{Describe(vista, now)}.");
        }

        var summary = $"Dry run: {ordered.Length} vista(s) left in your plan across {zones.Count} zone(s), {open} open now and {waiting} waiting for their hour or weather. Travel and emotes are not built yet.";
        Diag(summary);
        Svc.Chat.Print($"{AslConstants.LogPrefix} {summary}");
    }

    private static string Describe(in Vista vista, long now)
    {
        if (!VistaLog.TryGetWindow(vista, now, out var window))
        {
            return ", no window in sight";
        }

        if (window.IsEndless)
        {
            return string.Empty;
        }

        return window.IsOpenAt(now)
            ? $", closes in {TimeSpan.FromSeconds(window.End - now)}"
            : $", opens in {TimeSpan.FromSeconds(window.Start - now)}";
    }
}
