using AutoSightseeingLog.Core.Game.Ops;
using AutoSightseeingLog.Core.Ipc;
using AutoSightseeingLog.Core.Time;
using AutoSightseeingLog.Core.Vistas;
using Dalamud.Game.ClientState.Conditions;
using ECommons.DalamudServices;
using System.Numerics;
using System.Threading.Tasks;

namespace AutoSightseeingLog.Core.Tasks;

internal enum VistaOutcome : byte
{
    Recorded,
    NotRecorded,
    WindowClosed,
    Cancelled,
}

internal abstract partial class AutoCommon
{
    private const float ApproachArriveMeters = 2.5f;
    private const float StandingToleranceMeters = 0.8f;
    private const float WalkArrivedSlackMeters = 1f;
    // Rooms and halls the mount cannot enter: the ride stops this far out and the rest of the way is walked.
    private const float IndoorsWalkMeters = 45f;
    private const float LogPointSearchMeters = 4f;
    private const int WalkUpWatchdogMs = 45_000;
    private const int EmoteReadyWaitMs = 60_000;
    // The log records a vista a moment after the emote starts; past this the emote did not count.
    private const int EmoteRecordWaitMs = 6_000;
    private const int EmoteRecordPollFrames = 15;
    private const int EmoteRefusedRetryMs = 1_000;
    private const int MaxEmoteAttempts = 3;
    private const int StillnessBeforeEmoteMs = 2_000;

    // True once the character stands on foot at the vista's approach point, or at its log point when it has none.
    protected async Task<bool> ReachVista(Vista vista, string scope)
    {
        var name = VistaRegistry.Name(vista.Number);
        var target = vista.ApproachPoint;
        var rideStop = vista.Approach == VistaApproach.Indoors ? IndoorsWalkMeters : ApproachArriveMeters;
        Diag($"{scope}: heading to #{vista.Number:000} {name} ({vista.Approach}), approach point {FormatPosition(target)}, log point {FormatPosition(vista.Position)}");
        if (!await TravelTo(vista.TerritoryId, target, rideStop) || CancelToken.IsCancellationRequested)
        {
            return false;
        }

        if (!await SafeDismount($"{scope}-dismount"))
        {
            Warn($"{scope}: could not dismount near {name} ({ConditionTag()})");
            return false;
        }

        return await WalkTo(target, ApproachArriveMeters, $"{scope}-walk", $"Walking up to {name}");
    }

    protected async Task<VistaOutcome> LogVista(Vista vista, string scope)
    {
        var name = VistaRegistry.Name(vista.Number);
        for (var attempt = 1; attempt <= MaxEmoteAttempts; attempt++)
        {
            if (CancelToken.IsCancellationRequested)
            {
                return VistaOutcome.Cancelled;
            }

            if (VistaLog.CheckRecorded(vista.Number))
            {
                return VistaOutcome.Recorded;
            }

            if (!IsOpenNow(vista))
            {
                Diag($"{scope}: the window for {name} closed before the emote");
                return VistaOutcome.WindowClosed;
            }

            var attemptScope = $"{scope}-emote#{attempt}";
            if (!await ReadyForEmote(attemptScope))
            {
                continue;
            }

            if (!EmoteOps.CanUse(vista.EmoteId))
            {
                Diag($"{attemptScope}: the game does not allow emote {vista.EmoteId} right now ({ConditionTag()})");
                await DelayMs(EmoteRefusedRetryMs);
                continue;
            }

            Status = $"Taking in the view at {name}";
            EmoteOps.Execute(vista.EmoteId);
            Diag($"{attemptScope}: emote {vista.EmoteId} performed {DistanceTo(vista.Position):F1}m from the log point");
            if (await WaitUntilTimed(() => VistaLog.CheckRecorded(vista.Number), EmoteRecordWaitMs, $"{attemptScope}-record", EmoteRecordPollFrames))
            {
                return VistaOutcome.Recorded;
            }

            await StepTowardLogPoint(vista, attemptScope);
        }

        return VistaOutcome.NotRecorded;
    }

    private async Task<bool> WalkTo(Vector3 destination, float tolerance, string scope, string label)
    {
        if (DistanceTo(destination) <= tolerance + WalkArrivedSlackMeters)
        {
            return true;
        }

        Status = label;
        var operation = new MoveOp(move => move.MoveInZone(destination, walkMovement.WithTolerance(tolerance), null));
        await RunCancellable(operation, WalkUpWatchdogMs, scope, StuckDetector.MoveStallAbort(scope));
        if (operation.Fault is { } fault)
        {
            Diag($"{scope}: faulted: {fault.Message}");
        }

        var distance = DistanceTo(destination);
        if (distance <= tolerance + WalkArrivedSlackMeters)
        {
            return true;
        }

        Diag($"{scope}: stopped {distance:F1}m short of {FormatPosition(destination)}");
        return false;
    }

    private async Task<bool> ReadyForEmote(string scope)
    {
        if (Svc.Condition[ConditionFlag.InCombat])
        {
            Status = "Waiting for combat to end";
            Diag($"{scope}: in combat; waiting before the emote");
            if (!await WaitUntilTimed(() => !Svc.Condition[ConditionFlag.InCombat], EmoteReadyWaitMs, $"{scope}-combat"))
            {
                return false;
            }
        }

        if (Svc.Condition[ConditionFlag.Mounted] && !await SafeDismount($"{scope}-dismount"))
        {
            return false;
        }

        NavmeshIPC.Instance.Stop();
        return await WaitForStillness(StillnessBeforeEmoteMs);
    }

    // The log does not always record a vista from where its point sits, so one that did not count is tried again from
    // the reachable floor closest to the log point.
    private async Task StepTowardLogPoint(Vista vista, string scope)
    {
        var closer = NavmeshIPC.Instance.NearestPointReachable(vista.Position, LogPointSearchMeters, LogPointSearchMeters) ?? vista.Position;
        if (DistanceTo(closer) <= StandingToleranceMeters)
        {
            return;
        }

        Diag($"{scope}: not recorded; stepping {DistanceTo(closer):F1}m closer to the log point");
        await WalkTo(closer, StandingToleranceMeters, $"{scope}-closer", $"Stepping closer to {VistaRegistry.Name(vista.Number)}");
    }

    private static bool IsOpenNow(in Vista vista)
    {
        var now = EorzeaTime.Now();
        return VistaLog.TryGetWindow(vista, now, out var window) && window.IsOpenAt(now);
    }
}
