using AutoSightseeingLog.Core.Game.Ops;
using AutoSightseeingLog.Core.Ipc;
using AutoSightseeingLog.Core.Time;
using AutoSightseeingLog.Core.Travel;
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
    // The character was moved away mid-visit; the run comes back to it.
    Displaced,
    Cancelled,
}

internal abstract partial class AutoCommon
{
    private const float ApproachArriveMeters = 2.5f;
    private const float StandingToleranceMeters = 0.35f;
    private const float WantedMarginMeters = 0.25f;
    private const float WantedMarginFraction = 0.5f;
    private const float CentredMarginFraction = 0.9f;
    private const float StepToleranceMeters = 0.05f;
    private const int StepIntoWaitMs = 4_000;
    private const float DisplacedMeters = 20f;
    private const float WalkArrivedSlackMeters = 0.25f;
    // A walk that ends further out than this lost its path; nearer, the emote attempts step in the rest of the way.
    private const float ReachedMeters = 4f;
    // Rooms and halls the mount cannot enter: the ride stops this far out and the rest of the way is walked.
    private const float IndoorsWalkMeters = 45f;
    private const float LogPointSearchMeters = 4f;
    private const int WalkUpWatchdogMs = 45_000;
    private const int EmoteReadyWaitMs = 60_000;
    // The log records a vista a moment after the emote starts; past this the emote did not count.
    private const int EmoteRecordWaitMs = 6_000;
    private const int EmoteRecordPollFrames = 15;
    private const int EmoteAllowedWaitMs = 5_000;
    private const int MaxEmoteAttempts = 3;
    private const int StillnessBeforeEmoteMs = 2_000;
    // The game ignores the emote while the sit-down animation is still playing.
    private const int PoseSettleMs = 1_500;
    private const int StandUpWaitMs = 3_000;
    private const int StandUpAttempts = 2;

    protected async Task<bool> ReachVista(Vista vista, string scope)
    {
        var name = VistaRegistry.Name(vista.Number);
        var volume = VistaVolumes.Of(vista);
        VistaSpot.Show(volume, vista.TerritoryId);
        if (JumpRoutes.TryGet(vista.Number, out var route))
        {
            Diag($"{scope}: heading to #{vista.Number:000} {name} by its {route.Length}-leg jump route from {FormatPosition(route[0].Point)}, log point {FormatPosition(vista.Position)}, {DescribeVolume(volume)}");
            return await ReachByJumpRoute(vista, volume, route, name, scope);
        }

        var target = vista.ApproachPoint;
        var rideStop = vista.Approach == VistaApproach.Indoors ? IndoorsWalkMeters : ApproachArriveMeters;
        Diag($"{scope}: heading to #{vista.Number:000} {name} ({vista.Approach}), approach point {FormatPosition(target)}, log point {FormatPosition(vista.Position)}, {DescribeVolume(volume)}");
        if (!await TravelTo(vista.TerritoryId, target, rideStop) || CancelToken.IsCancellationRequested)
        {
            return false;
        }

        return await StandInside(vista, volume, name, scope);
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

            if (!IsAt(vista))
            {
                Diag($"{scope}: the character is no longer at {name}");
                return VistaOutcome.Displaced;
            }

            var volume = VistaVolumes.Of(vista);
            var attemptScope = $"{scope}-emote#{attempt}";
            if (!await ReadyForEmote(attemptScope))
            {
                continue;
            }

            // Right after landing or walking the game refuses emotes for a moment, and that wait is not a failed attempt.
            if (!await WaitUntilTimed(() => EmoteOps.CanUse(vista.EmoteId), EmoteAllowedWaitMs, $"{attemptScope}-allowed", EmoteRecordPollFrames))
            {
                Diag($"{attemptScope}: the game does not allow emote {vista.EmoteId} right now ({ConditionTag()})");
                continue;
            }

            Status = $"Taking in the view at {name}";
            EmoteOps.Execute(vista.EmoteId);
            DescribeEmote(vista, volume, attemptScope);
            if (await WaitUntilTimed(() => VistaLog.CheckRecorded(vista.Number), EmoteRecordWaitMs, $"{attemptScope}-record", EmoteRecordPollFrames))
            {
                if (EmoteOps.EntersPose(vista.EmoteId))
                {
                    await LeavePose(vista.EmoteId, attemptScope);
                }

                return VistaOutcome.Recorded;
            }

            await StepTowardLogPoint(vista, volume, attemptScope);
        }

        return VistaOutcome.NotRecorded;
    }

    // The log can miss an emote from 2.5 m that it takes from under 1 m, so the walk ends on the reachable floor closest
    // to the point rather than within the travel tolerance.
    private async Task<bool> WalkUpTo(Vector3 target, string scope, string name)
    {
        var stand = NavmeshIPC.Instance.NearestPointReachable(target, LogPointSearchMeters, LogPointSearchMeters) ?? target;
        await WalkTo(stand, StandingToleranceMeters, scope, $"Walking up to {name}");
        var distance = DistanceTo(target);
        if (distance <= ReachedMeters)
        {
            return true;
        }

        Diag($"{scope}: stopped {distance:F1}m from {FormatPosition(target)}");
        return false;
    }

    private async Task WalkTo(Vector3 destination, float tolerance, string scope, string label)
    {
        if (DistanceTo(destination) <= tolerance + WalkArrivedSlackMeters)
        {
            return;
        }

        Status = label;
        var operation = new MoveOp(move => move.MoveInZone(destination, walkMovement.WithTolerance(tolerance), null));
        await RunCancellable(operation, WalkUpWatchdogMs, scope, StuckDetector.MoveStallAbort(scope));
        if (operation.Fault is { } fault)
        {
            Diag($"{scope}: faulted: {fault.Message}");
        }
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
        if (await WaitForStillness(StillnessBeforeEmoteMs))
        {
            return true;
        }

        Diag($"{scope}: the character would not stand still ({ConditionTag()})");
        return false;
    }

    // Everything the log checks, as the game has it at the emote, next to the forecast the plan was built on.
    private void DescribeEmote(in Vista vista, in VistaVolume volume, string scope)
    {
        var now = EorzeaTime.Now();
        var weather = WeatherOps.Current();
        var forecast = ZoneWeather.WeatherAt(vista.TerritoryId, now);
        var weatherText = weather == forecast
            ? GameNames.Weather(weather)
            : $"{GameNames.Weather(weather)} (forecast {GameNames.Weather(forecast)})";
        Diag($"{scope}: emote {vista.EmoteId} performed {DistanceTo(vista.Position):F2}m from the log point ({GroundDistanceTo(vista.Position):F2}m across), {DescribeStanding(volume)}, at {EorzeaTime.BellOf(now):00}:{EorzeaTime.MinuteOf(now):00} ET in {weatherText}");
    }

    // The log does not always record a vista from where its point sits, so one that did not count is tried again from
    // the reachable floor closest to the log point, then from the point itself.
    private async Task StepTowardLogPoint(Vista vista, VistaVolume volume, string scope)
    {
        if (IsStandingInside(volume))
        {
            Diag($"{scope}: not recorded from inside the volume; stepping to its centre");
            await StepInto(volume, vista.Position, volume.NarrowestHalfWidth * CentredMarginFraction, scope);
            return;
        }

        var closer = NavmeshIPC.Instance.NearestPointReachable(vista.Position, LogPointSearchMeters, LogPointSearchMeters) ?? vista.Position;
        if (DistanceTo(closer) > StandingToleranceMeters + WalkArrivedSlackMeters)
        {
            Diag($"{scope}: not recorded; stepping {DistanceTo(closer):F1}m closer to the log point");
            await WalkTo(closer, StandingToleranceMeters, $"{scope}-closer", $"Stepping closer to {VistaRegistry.Name(vista.Number)}");
        }

        await StepInto(volume, vista.Position, volume.NarrowestHalfWidth * CentredMarginFraction, scope);
    }

    // A teleport cast before a pause, or the player, can move the character away mid-visit.
    private bool IsAt(in Vista vista)
        => Svc.ClientState.TerritoryType == vista.TerritoryId
        && MathF.Min(DistanceTo(vista.Position), DistanceTo(vista.ApproachPoint)) <= DisplacedMeters;

    private static float GroundDistanceTo(Vector3 point)
        => Svc.Objects.LocalPlayer is { } player ? GroundDistance.Between(player.Position, point) : float.MaxValue;

    // Walking stands a seated character up, but the next leg may start by mounting, so the pose is left first by using
    // its emote again.
    private async Task LeavePose(ushort emoteId, string scope)
    {
        for (var attempt = 1; attempt <= StandUpAttempts; attempt++)
        {
            await DelayMs(PoseSettleMs);
            if (CancelToken.IsCancellationRequested || !EmoteOps.InPose())
            {
                return;
            }

            Status = "Standing up";
            EmoteOps.Execute(emoteId);
            if (await WaitUntilTimed(static () => !EmoteOps.InPose(), StandUpWaitMs, $"{scope}-stand#{attempt}", EmoteRecordPollFrames))
            {
                Diag($"{scope}: stood up from the pose");
                return;
            }
        }

        Warn($"{scope}: still seated after using emote {emoteId} again; the next leg starts from the pose");
    }

    private static bool IsOpenNow(in Vista vista)
    {
        var now = EorzeaTime.Now();
        return VistaLog.TryGetWindow(vista, now, out var window) && window.IsOpenAt(now);
    }
}
