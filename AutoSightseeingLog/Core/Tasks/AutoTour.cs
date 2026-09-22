using AutoSightseeingLog.Core.Ipc;
using AutoSightseeingLog.Core.Time;
using AutoSightseeingLog.Core.Vistas;
using Dalamud.Game.ClientState.Conditions;
using ECommons.DalamudServices;
using System.Text;
using System.Threading.Tasks;

namespace AutoSightseeingLog.Core.Tasks;

// Works the plan one vista at a time, always the most urgent one left: open vistas before they close, then the one whose
// window opens soonest, waited out on the spot.
internal sealed class AutoTour(TourSession session, TourProgress progress) : AutoCommon
{
    // A window further out than this is left for a later run; waiting on the spot longer rarely beats coming back.
    private const long WindowWaitLimitSeconds = 20 * TimeUnits.SecondsPerMinute;
    private const int WindowPollMs = 1_000;
    private const int MaxVisitsPerVista = 3;
    private const int SettleWaitMs = 15_000;

    private readonly Dictionary<ushort, int> visits = new();
    private readonly List<ushort> leftToPlayer = [];
    private readonly List<ushort> notLogged = [];
    private readonly List<ushort> remaining = [];

    protected override async Task Execute()
    {
        progress.SetPhase(TourPhase.Reading);
        Status = "Reading your Sightseeing Log…";
        VistaLog.Refresh(force: true);
        await HoldCombatMovementAndSettle("tour");
        try
        {
            // A teleport cast when the run was paused still lands, so the run starts from wherever it takes the character.
            await WaitUntilTimed(static () => !Svc.Condition[ConditionFlag.Casting] && !Svc.Condition[ConditionFlag.BetweenAreas], SettleWaitMs, "tour-settle");
            if (!VistaLog.LogUnlocked)
            {
                progress.SetPhase(TourPhase.Unlocking);
                if (!await UnlockSightseeingLog())
                {
                    return;
                }
            }

            await WorkPlan();
        }
        finally
        {
            NavmeshIPC.Instance.Stop();
            ReleaseCombatMovement("tour");
            progress.ClearVista();
            VistaSpot.Clear();
        }
    }

    private async Task WorkPlan()
    {
        while (!CancelToken.IsCancellationRequested)
        {
            var now = EorzeaTime.Now();
            var ordered = VistaPlan.Order(CollectRemaining(), now);
            progress.SetQueue(ordered);
            if (!Svc.ClientState.IsLoggedIn)
            {
                Warn("the character logged out; ending the run");
                Report(ordered, now);
                return;
            }

            if (ordered.Length == 0
                || !VistaRegistry.TryGet(ordered[0], out var next)
                || !VistaLog.TryGetWindow(next, now, out var window)
                || (!window.IsOpenAt(now) && window.Start - now > WindowWaitLimitSeconds))
            {
                session.EndedOnItsOwn = true;
                Report(ordered, now);
                return;
            }

            await Visit(next);
        }
    }

    private List<ushort> CollectRemaining()
    {
        remaining.Clear();
        var plan = session.Plan;
        for (var index = 0; index < plan.Count; index++)
        {
            var number = plan[index];
            if (VistaLog.IsRecorded(number)
                || leftToPlayer.Contains(number)
                || notLogged.Contains(number)
                || visits.GetValueOrDefault(number) >= MaxVisitsPerVista)
            {
                continue;
            }

            remaining.Add(number);
        }

        return remaining;
    }

    private async Task Visit(Vista vista)
    {
        var number = vista.Number;
        var name = VistaRegistry.Name(number);
        var scope = $"vista#{number:000}";
        visits[number] = visits.GetValueOrDefault(number) + 1;
        progress.SetVista(number);

        if (vista.Approach == VistaApproach.NpcGate || (vista.Approach == VistaApproach.JumpPuzzle && !JumpRoutes.Has(number)))
        {
            Diag($"{scope}: {name} is reached {(vista.Approach == VistaApproach.JumpPuzzle ? "by a jump puzzle" : "through an NPC")}; leaving it to the player");
            leftToPlayer.Add(number);
            return;
        }

        progress.SetPhase(TourPhase.Travelling);
        if (!await ReachVista(vista, scope))
        {
            if (!CancelToken.IsCancellationRequested && Svc.ClientState.IsLoggedIn)
            {
                Warn($"{scope}: could not reach {name}");
                notLogged.Add(number);
            }

            return;
        }

        if (!await WaitForWindow(vista, name, scope))
        {
            return;
        }

        progress.SetPhase(TourPhase.Emoting);
        switch (await LogVista(vista, scope))
        {
            case VistaOutcome.Recorded:
                session.Sample();
                Diag($"{scope}: {name} is in the log");
                Svc.Chat.Print($"{AslConstants.LogPrefix} Logged #{number:000} {name}.");
                return;
            case VistaOutcome.NotRecorded:
                Warn($"{scope}: the log did not record {name}");
                notLogged.Add(number);
                return;
        }
    }

    // True once the window is open with the character on the spot; false leaves the vista in the plan for later.
    private async Task<bool> WaitForWindow(Vista vista, string name, string scope)
    {
        var announced = false;
        while (!CancelToken.IsCancellationRequested)
        {
            var now = EorzeaTime.Now();
            if (!VistaLog.TryGetWindow(vista, now, out var window))
            {
                return false;
            }

            if (window.IsOpenAt(now))
            {
                return true;
            }

            var wait = window.Start - now;
            if (wait > WindowWaitLimitSeconds)
            {
                Diag($"{scope}: {name}'s next window opens in {TimeSpan.FromSeconds(wait)}; leaving it for later");
                return false;
            }

            if (!announced)
            {
                progress.SetPhase(TourPhase.Waiting);
                Diag($"{scope}: waiting {TimeSpan.FromSeconds(wait)} on the spot for {name}'s window");
                Svc.Chat.Print($"{AslConstants.LogPrefix} Waiting at #{vista.Number:000} {name}: its window opens in {TimeSpan.FromSeconds(wait):m\\:ss}.");
                announced = true;
            }

            Status = $"Waiting at {name}: opens in {TimeSpan.FromSeconds(wait):h\\:mm\\:ss}";
            await DelayMs(WindowPollMs);
        }

        return false;
    }

    private void Report(ushort[] waiting, long now)
    {
        var summary = new StringBuilder();
        summary.Append($"Tour finished: {session.VistasLogged} vista(s) logged.");
        if (leftToPlayer.Count > 0)
        {
            summary.Append(" Left to you: ").Append(Describe(leftToPlayer)).Append('.');
        }

        if (notLogged.Count > 0)
        {
            summary.Append(" Not logged: ").Append(Describe(notLogged)).Append(" (the plugin log has the details).");
        }

        if (waiting.Length > 0 && VistaRegistry.TryGet(waiting[0], out var soonest) && VistaLog.TryGetWindow(soonest, now, out var window))
        {
            summary.Append($" {waiting.Length} wait(s) for a later window; the next opens in {Minutes(window.Start - now)}.");
        }

        var text = summary.ToString();
        Diag(text);
        Svc.Chat.Print($"{AslConstants.LogPrefix} {text}");
    }

    private static string Minutes(long seconds)
    {
        var minutes = Math.Max(0, (seconds + TimeUnits.SecondsPerMinute - 1) / TimeUnits.SecondsPerMinute);
        return minutes >= 60 ? $"{minutes / 60} h {minutes % 60:00} min" : $"{minutes} min";
    }

    private static string Describe(List<ushort> numbers)
    {
        var text = new StringBuilder();
        for (var index = 0; index < numbers.Count; index++)
        {
            if (index > 0)
            {
                text.Append(", ");
            }

            text.Append($"#{numbers[index]:000} {VistaRegistry.Name(numbers[index])}");
        }

        return text.ToString();
    }
}
