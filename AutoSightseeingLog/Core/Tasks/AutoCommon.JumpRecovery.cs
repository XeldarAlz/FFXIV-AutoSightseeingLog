using AutoSightseeingLog.Core.Game.Ops;
using AutoSightseeingLog.Core.Ipc;
using AutoSightseeingLog.Core.Vistas;
using ECommons.DalamudServices;
using System.Numerics;
using System.Threading.Tasks;

namespace AutoSightseeingLog.Core.Tasks;

internal abstract partial class AutoCommon
{
    private async Task<int> RestartJumpRoute(JumpRoute route, string name, string scope)
    {
        NavmeshIPC.Instance.Stop();
        if (!await WaitForJumpGrounding(scope) || CancelToken.IsCancellationRequested)
        {
            return NotOnRoute;
        }

        // Never ask ground navigation to reach the entrance from an unsupported elevated roof.
        // The climb owns the retry budget and calibration; this reset does not create a new climb.
        var territory = Svc.ClientState.TerritoryType;
        Status = "Returning to the jump puzzle entrance";
        Diag($"{scope}: no verified local return from {FormatPrecise(Svc.Objects.LocalPlayer?.Position ?? Vector3.Zero)}; teleporting to retry from the entrance");
        if (!await TryTeleportShortcut(territory, route[0].Point, $"{scope}-reset", TeleportPurpose.JumpRoute)
            || CancelToken.IsCancellationRequested || !await WaitForJumpGrounding(scope))
        {
            if (!CancelToken.IsCancellationRequested)
            {
                Warn($"{scope}: the jump-route reset could not complete its teleport; preserving the current foothold");
            }
            return NotOnRoute;
        }

        if (CancelToken.IsCancellationRequested || Svc.ClientState.TerritoryType != territory
            || Svc.Objects.LocalPlayer is null || JumpPhysics.IsAirborne())
        {
            return NotOnRoute;
        }

        return await WalkToRouteStart(territory, route[0].Point, name, scope) ? 0 : NotOnRoute;
    }

    private async Task<int> ReturnToRoofTakeoff(JumpRoute route, int lastReachedLeg, string scope)
    {
        if (CancelToken.IsCancellationRequested || Svc.Objects.LocalPlayer is not { } player || JumpPhysics.IsAirborne())
        {
            return NotOnRoute;
        }

        var from = player.Position;
        foreach (var leg in JumpRecoveryPath.Checkpoints(route, lastReachedLeg))
        {
            if (!JumpFooting.TryRecoveryPath(from, route[leg].Point, out var path, out var failure) || path is null)
            {
                if (failure != JumpRecoveryFailure.TooFar)
                {
                    Diag($"{scope}: roof return to leg {leg + 1} has no supported walk ({failure})");
                }
                continue;
            }

            Status = "Returning to the roof takeoff";
            Diag($"{scope}: returning from {FormatPrecise(from)} to roof takeoff at leg {leg + 1}/{route.Length} over {path.Points.Count} verified floor samples");
            // Once movement starts, do not try a different path from the now-stale starting point.
            return await WalkRoofRecovery(path, route[leg].Point, scope) ? leg : NotOnRoute;
        }

        return NotOnRoute;
    }

    private async Task<bool> WalkRoofRecovery(JumpRecoveryPath path, Vector3 target, string scope)
    {
        var navmesh = NavmeshIPC.Instance;
        var tolerance = navmesh.Tolerance();
        var wasWalking = WalkModeOps.IsWalking();
        var stall = new MoveStallTracker();
        var lostFooting = false;
        try
        {
            navmesh.Stop();
            navmesh.SetTolerance(0.1f);
            WalkModeOps.SetWalking(true);
            navmesh.MoveAlong(path.Points.Skip(1).ToList(), false);
            var finished = await WaitUntilTimed(() =>
            {
                var sample = SampleJump();
                lostFooting = Svc.Objects.LocalPlayer is null || sample.Airborne || !path.Contains(sample.Position);
                return lostFooting || !navmesh.IsRunning() || stall.Check() != StallKind.None;
            }, 25_000, scope, 1);
            navmesh.Stop();
            var grounded = await WaitForJumpGrounding(scope);
            var reached = finished && grounded && !lostFooting && !CancelToken.IsCancellationRequested
                && HasLandedOn(target, JumpPosition.AlignMeters);
            Diag($"{scope}: roof return {(reached ? "reached the takeoff; retrying the climb" : "stopped before reaching the takeoff")}, {GroundDistanceTo(target):F2}m across, {HeightAbove(target):+0.00;-0.00}m high, lost footing {lostFooting}");
            return reached;
        }
        finally
        {
            navmesh.Stop();
            navmesh.SetTolerance(tolerance);
            WalkModeOps.SetWalking(wasWalking);
        }
    }
}
