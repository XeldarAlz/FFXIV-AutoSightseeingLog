using AutoSightseeingLog.Core.Game.Ops;
using AutoSightseeingLog.Core.Ipc;
using AutoSightseeingLog.Core.Travel;
using AutoSightseeingLog.Core.Vistas;
using Dalamud.Game.ClientState.Conditions;
using ECommons.DalamudServices;
using System.Numerics;
using System.Threading.Tasks;

namespace AutoSightseeingLog.Core.Tasks;

internal abstract partial class AutoCommon
{
    private const float StepIntoMaxAcrossMeters = 3f;
    private const float StepIntoMaxRiseMeters = 1f;
    private const float HoverMeters = 2f;
    private const float HoverNudgeMaxMeters = 3f;
    private const int HoverNudgeWaitMs = 4_000;
    private const float HoverAcrossMinMeters = 0.1f;
    private const float HoverAcrossMaxMeters = 0.4f;
    private const float HoverVerticalSlackMeters = 1f;
    private const int HoverFlightWaitMs = 30_000;
    private const int PathIdleConfirmMs = 500;
    private const int MountWatchdogMs = 10_000;
    private const int PlayerFootingWaitMs = 60_000;
    private const int PlayerFootingPollFrames = 10;

    private async Task<bool> StandInside(Vista vista, VistaVolume volume, string name, string scope)
    {
        var found = VistaFooting.TryFind(volume, out var footing);
        Diag(found
            ? $"{scope}: footing at {FormatPrecise(footing.Point)}, {footing.Margin:F2}m inside the volume, {(footing.Walkable ? $"walkable from {FormatPrecise(footing.WalkFrom)}" : "off the walkable floor")}, {footing.Candidates} candidate(s)"
            : $"{scope}: no standable surface inside the volume");
        if (found)
        {
            VistaSpot.ShowFooting(footing.Point);
        }

        if (found && footing.Walkable)
        {
            await WalkOnto(volume, footing, name, scope);
        }
        else if (found && CanLandOn(vista))
        {
            await LandOnto(volume, footing, name, scope);
        }
        else
        {
            await WalkNear(vista, volume, name, scope);
        }

        if (CancelToken.IsCancellationRequested)
        {
            return false;
        }

        if (IsStandingInside(volume))
        {
            Diag($"{scope}: {DescribeStanding(volume)}");
            return true;
        }

        Diag($"{scope}: the approach ended {DescribeStanding(volume)}");
        if (await AwaitPlayerFooting(vista, volume, name, scope))
        {
            return true;
        }

        if (CancelToken.IsCancellationRequested)
        {
            return false;
        }

        var near = DistanceTo(vista.Position) <= ReachedMeters;
        Diag(near
            ? $"{scope}: still outside the volume; trying the emote from here anyway"
            : $"{scope}: still {DistanceTo(vista.Position):F1}m from the log point");
        return near;
    }

    private async Task WalkOnto(VistaVolume volume, Footing footing, string name, string scope)
    {
        if (!await SafeDismount($"{scope}-dismount"))
        {
            Warn($"{scope}: could not dismount near {name} ({ConditionTag()})");
            return;
        }

        await WalkTo(footing.WalkFrom, StandingToleranceMeters, $"{scope}-walk", $"Walking up to {name}");
        await StepInto(volume, footing.Point, WantedMargin(footing.Margin), $"{scope}-walk");
    }

    private async Task WalkNear(Vista vista, VistaVolume volume, string name, string scope)
    {
        if (!await SafeDismount($"{scope}-dismount"))
        {
            Warn($"{scope}: could not dismount near {name} ({ConditionTag()})");
            return;
        }

        if (!await WalkUpTo(vista.ApproachPoint, $"{scope}-walk", name))
        {
            return;
        }

        await StepInto(volume, vista.Position, volume.NarrowestHalfWidth * CentredMarginFraction, $"{scope}-walk");
    }

    private async Task LandOnto(VistaVolume volume, Footing footing, string name, string scope)
    {
        Status = $"Landing on {name}";
        if (!Svc.Condition[ConditionFlag.Mounted])
        {
            await RunCancellable(new MoveOp(move => move.MountNow()), MountWatchdogMs, $"{scope}-mount");
        }

        if (!Svc.Condition[ConditionFlag.Mounted] || CancelToken.IsCancellationRequested)
        {
            Diag($"{scope}: could not mount to land on the footing ({ConditionTag()})");
            return;
        }

        var hover = footing.Point with { Y = footing.Point.Y + HoverMeters };
        var hoverAcross = Math.Clamp(footing.Margin * WantedMarginFraction, HoverAcrossMinMeters, HoverAcrossMaxMeters);
        if (!await FlyToHover(hover, hoverAcross, scope))
        {
            return;
        }

        Diag($"{scope}: hovering {GroundDistanceTo(footing.Point):F2}m across from the footing ({ConditionTag()})");
        if (!await DescendAndDismount($"{scope}-descend"))
        {
            return;
        }

        await StepInto(volume, footing.Point, WantedMargin(footing.Margin), $"{scope}-land");
    }

    private async Task<bool> FlyToHover(Vector3 hover, float hoverAcross, string scope)
    {
        var navmesh = NavmeshIPC.Instance;
        var tolerance = navmesh.Tolerance();
        var idleSince = 0L;
        bool FlightEnded()
        {
            if (IsHoveringOver(hover, hoverAcross))
            {
                return true;
            }

            if (navmesh.IsBusy())
            {
                idleSince = 0L;
                return false;
            }

            if (idleSince == 0L)
            {
                idleSince = Environment.TickCount64;
            }

            return Environment.TickCount64 - idleSince >= PathIdleConfirmMs;
        }

        navmesh.SetTolerance(HoverAcrossMinMeters);
        try
        {
            if (!navmesh.PathfindAndMoveTo(hover, fly: true))
            {
                Diag($"{scope}: the pathfinder refused the flight to {FormatPrecise(hover)}");
                return false;
            }

            await WaitUntilTimed(FlightEnded, HoverFlightWaitMs, $"{scope}-hover", 1);
            if (!IsHoveringOver(hover, hoverAcross) && DistanceTo(hover) <= HoverNudgeMaxMeters && Svc.Condition[ConditionFlag.InFlight])
            {
                navmesh.MoveAlong([hover], fly: true);
                await WaitUntilTimed(() => IsHoveringOver(hover, hoverAcross) || !navmesh.IsRunning(), HoverNudgeWaitMs, $"{scope}-nudge", 1);
            }
        }
        finally
        {
            navmesh.Stop();
            navmesh.SetTolerance(tolerance);
        }

        return !CancelToken.IsCancellationRequested;
    }

    private async Task StepInto(VistaVolume volume, Vector3 point, float wantedMargin, string scope)
    {
        if (Svc.Objects.LocalPlayer is not { } player || HasFooting(volume, wantedMargin))
        {
            return;
        }

        var across = GroundDistance.Between(player.Position, point);
        if (across > StepIntoMaxAcrossMeters || MathF.Abs(player.Position.Y - point.Y) > StepIntoMaxRiseMeters)
        {
            return;
        }

        var navmesh = NavmeshIPC.Instance;
        var tolerance = navmesh.Tolerance();
        var wasWalking = WalkModeOps.IsWalking();
        navmesh.SetTolerance(StepToleranceMeters);
        WalkModeOps.SetWalking(true);
        try
        {
            navmesh.MoveAlong([point], false);
            await WaitUntilTimed(() => HasFooting(volume, wantedMargin) || !navmesh.IsRunning(), StepIntoWaitMs, $"{scope}-into", 1);
        }
        finally
        {
            navmesh.Stop();
            navmesh.SetTolerance(tolerance);
            WalkModeOps.SetWalking(wasWalking);
        }

        Diag($"{scope}: stepped from {across:F2}m across to {DescribeStanding(volume)}");
    }

    private async Task<bool> AwaitPlayerFooting(Vista vista, VistaVolume volume, string name, string scope)
    {
        if (!Plugin.Instance.Configuration.AskAtHardSpots)
        {
            return false;
        }

        NavmeshIPC.Instance.Stop();
        var seconds = PlayerFootingWaitMs / TimeUnits.MillisecondsPerSecond;
        Diag($"{scope}: asking the player to stand inside the volume, waiting {seconds}s");
        var spot = $"#{vista.Number:000} {name}";
        var request = $"I cannot get onto this spot myself. Stand inside the marked box within {seconds}s and I will do the rest.";
        Svc.Chat.Print($"{AslConstants.LogPrefix} {spot}: {request}");
        Status = $"Stand inside the marked spot at {name}";
        VistaSpot.SetAwaitingPlayer(true);
        var notification = AttentionOps.Ask(spot, request, TimeSpan.FromMilliseconds(PlayerFootingWaitMs));
        try
        {
            return await WaitUntilTimed(() => IsStandingInside(volume), PlayerFootingWaitMs, $"{scope}-player", PlayerFootingPollFrames);
        }
        finally
        {
            notification.DismissNow();
            VistaSpot.SetAwaitingPlayer(false);
        }
    }

    private static bool CanLandOn(in Vista vista)
        => vista.Approach != VistaApproach.Indoors && FlightOps.UnlockedIn(vista.TerritoryId);

    private static bool IsStandingInside(in VistaVolume volume)
        => Svc.Objects.LocalPlayer is { } player
        && volume.Contains(player.Position)
        && !Svc.Condition[ConditionFlag.Mounted]
        && !Svc.Condition[ConditionFlag.Jumping];

    private static bool HasFooting(in VistaVolume volume, float wantedMargin)
        => Svc.Objects.LocalPlayer is { } player
        && volume.VerticalMargin(player.Position) >= 0f
        && volume.HorizontalMargin(player.Position) >= wantedMargin;

    private static bool IsHoveringOver(Vector3 hover, float hoverAcross)
        => Svc.Objects.LocalPlayer is { } player
        && GroundDistance.Between(player.Position, hover) <= hoverAcross
        && MathF.Abs(player.Position.Y - hover.Y) <= HoverVerticalSlackMeters;

    private static float WantedMargin(float availableMargin)
        => MathF.Min(WantedMarginMeters, availableMargin * WantedMarginFraction);

    private static string DescribeVolume(in VistaVolume volume)
        => $"{(volume.IsCylinder ? "cylinder" : "box")} volume half ({volume.HalfExtents.X:F2}, {volume.HalfExtents.Y:F2}, {volume.HalfExtents.Z:F2}), {volume.Bottom:F2} to {volume.Top:F2} high";

    private static string DescribeStanding(in VistaVolume volume)
    {
        if (Svc.Objects.LocalPlayer is not { } player)
        {
            return "with no character";
        }

        var local = volume.ToLocal(player.Position);
        return $"{(volume.Contains(player.Position) ? "inside" : "outside")} the volume at local ({local.X:F2}, {local.Y:F2}, {local.Z:F2}), margins {volume.HorizontalMargin(player.Position):F2}m across and {volume.VerticalMargin(player.Position):F2}m high";
    }

    private static string FormatPrecise(Vector3 position)
        => $"({position.X:F2}, {position.Y:F2}, {position.Z:F2})";
}
