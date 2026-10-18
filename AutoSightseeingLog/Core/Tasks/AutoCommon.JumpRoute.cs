using AutoSightseeingLog.Core.Game.Ops;
using AutoSightseeingLog.Core.Ipc;
using AutoSightseeingLog.Core.Vistas;
using ECommons.DalamudServices;
using System.Numerics;
using System.Threading.Tasks;

namespace AutoSightseeingLog.Core.Tasks;

internal abstract partial class AutoCommon
{
    private const int NotOnRoute = -1;
    private const int MaxRouteMisses = 8;
    private const int MaxMissesOnOneLeg = 3;
    private const int MaxWalksBackOntoRoute = 3;

    private async Task<bool> ReachByJumpRoute(Vista vista, VistaVolume volume, JumpRoute route, string name, string scope)
    {
        var inZone = Svc.ClientState.TerritoryType == vista.TerritoryId;
        if (inZone && IsStandingInside(volume))
        {
            Diag($"{scope}: already {DescribeStanding(volume)}");
            return true;
        }

        var startLeg = inZone ? NearestLegStoodOn(route) : NotOnRoute;
        if (startLeg == NotOnRoute && inZone && Svc.Objects.LocalPlayer is { } player
            && !JumpLanding.IsAtRouteGroundLevel(player.Position, route[0].Point)
            && JumpRecoveryPath.Checkpoints(route, route.Length - 1).Any(leg =>
                Vector3.Distance(player.Position, route[leg].Point) <= JumpRecoveryPath.MaxDistance))
        {
            NavmeshIPC.Instance.Stop();
            if (!await WaitForJumpGrounding(scope) || CancelToken.IsCancellationRequested)
            {
                return false;
            }

            // Starting automation during a fall may end on a known ledge or back at street level.
            startLeg = NearestLegStoodOn(route);
            if (startLeg == NotOnRoute && Svc.Objects.LocalPlayer is { } grounded
                && !JumpLanding.IsAtRouteGroundLevel(grounded.Position, route[0].Point))
            {
                startLeg = await ReturnToRoofTakeoff(route, route.Length - 1, $"{scope}-resume");
                if (startLeg == NotOnRoute)
                {
                    startLeg = await RestartJumpRoute(route, name, $"{scope}-resume");
                    if (startLeg == NotOnRoute)
                    {
                        return false;
                    }
                }
            }
        }

        if (startLeg == NotOnRoute)
        {
            if (!await WalkToRouteStart(vista.TerritoryId, route[0].Point, name, scope))
            {
                return false;
            }

            startLeg = 0;
        }
        else
        {
            Diag($"{scope}: already on the jump route, at leg {startLeg + 1}/{route.Length}");
        }

        if (!await ClimbJumpRoute(route, startLeg, name, scope))
        {
            return false;
        }

        await WalkStraightInto(volume, vista.Position, scope);
        return await StandInside(vista, volume, name, scope);
    }

    private async Task<bool> WalkToRouteStart(uint territoryId, Vector3 start, string name, string scope)
    {
        if (!await TravelTo(territoryId, start, ApproachArriveMeters) || CancelToken.IsCancellationRequested)
        {
            return false;
        }

        if (!await SafeDismount($"{scope}-dismount"))
        {
            Warn($"{scope}: could not dismount at the foot of the jump route to {name} ({ConditionTag()})");
            return false;
        }

        return await WalkUpTo(start, $"{scope}-walk", name);
    }

    private async Task<bool> ClimbJumpRoute(JumpRoute route, int startLeg, string name, string scope)
    {
        var navmesh = NavmeshIPC.Instance;
        var tolerance = navmesh.Tolerance();
        var wasWalking = WalkModeOps.IsWalking();
        navmesh.SetTolerance(route.ArriveMeters);
        WalkModeOps.SetWalking(false);
        try
        {
            Diag($"{scope}: jump controller checks precise takeoffs, verifies roof returns and teleports to retry when stranded");
            var leg = startLeg;
            var missesByLeg = new int[route.Length];
            var trajectory = new JumpTrajectory();
            for (var misses = 0; misses < MaxRouteMisses; misses++)
            {
                Status = $"Climbing to {name}";
                Diag($"{scope}: climbing from leg {leg + 1}/{route.Length} after {misses} miss(es)");
                var missedLeg = await RunLegs(route, leg, trajectory, scope);
                if (missedLeg == route.Length)
                {
                    Diag($"{scope}: at the top of the jump route");
                    return true;
                }

                if (CancelToken.IsCancellationRequested)
                {
                    return false;
                }

                var missesOnThatLeg = ++missesByLeg[missedLeg];
                if (missesOnThatLeg >= MaxMissesOnOneLeg)
                {
                    Warn($"{scope}: missed leg {missedLeg + 1}/{route.Length} of the jump route to {name} {missesOnThatLeg} times; stopping with launch diagnostics in the log");
                    return false;
                }

                if (misses + 1 >= MaxRouteMisses)
                {
                    break;
                }

                leg = await FindWayBackOnto(route, missedLeg, name, scope);
                if (leg == NotOnRoute)
                {
                    if (!CancelToken.IsCancellationRequested)
                    {
                        Warn($"{scope}: could not get back onto the jump route to {name} ({ConditionTag()})");
                    }

                    return false;
                }
            }

            Warn($"{scope}: gave up on the jump route to {name} after {MaxRouteMisses} misses");
            return false;
        }
        finally
        {
            navmesh.Stop();
            navmesh.SetTolerance(tolerance);
            WalkModeOps.SetWalking(wasWalking);
        }
    }

    private async Task<int> RunLegs(JumpRoute route, int startLeg, JumpTrajectory trajectory, string scope)
    {
        var correctionLeg = startLeg;
        for (var leg = startLeg; leg < route.Length; leg++)
        {
            if (CancelToken.IsCancellationRequested)
            {
                return leg;
            }

            var step = leg == startLeg
                ? JumpStep.Walk(route[leg].Point) with { LandingTolerance = route[leg].LandingTolerance }
                : route[leg];
            JumpStep? nextStep = leg + 1 < route.Length ? route[leg + 1] : null;
            if (!await RunLeg(step, route.Recorded, $"{scope}-leg{leg + 1}", trajectory, nextStep, correction: leg == correctionLeg && leg > 0))
            {
                if (!CancelToken.IsCancellationRequested && Svc.Objects.LocalPlayer is { } player)
                {
                    var followingWalk = JumpPosition.FollowingWalkLeg(route, leg, player.Position, JumpPhysics.IsAirborne());
                    if (followingWalk != NotOnRoute && JumpLanding.CanCorrect(player.Position, route[followingWalk].Point,
                        route.Recorded, JumpFooting.InspectWalk(player.Position, route[followingWalk].Point)))
                    {
                        Diag($"{scope}-leg{leg + 1}: grounded at following recorded walk leg {followingWalk + 1}; continuing from that foothold");
                        correctionLeg = followingWalk;
                        leg = followingWalk - 1;
                        continue;
                    }
                }

                return leg;
            }
        }

        return route.Length;
    }

    private async Task<int> FindWayBackOnto(JumpRoute route, int missedLeg, string name, string scope)
    {
        NavmeshIPC.Instance.Stop();
        if (!await WaitForJumpGrounding(scope) || CancelToken.IsCancellationRequested)
        {
            return NotOnRoute;
        }

        var stoodOn = NearestLegStoodOn(route, missedLeg);
        if (stoodOn != NotOnRoute)
        {
            Diag($"{scope}: still on the route at leg {stoodOn + 1}/{route.Length}");
            return stoodOn;
        }

        if (Svc.Objects.LocalPlayer is { } groundedPlayer
            && JumpLanding.RetainedFoothold(route, groundedPlayer.Position, missedLeg))
        {
            Diag($"{scope}: beside the recorded landing/takeoff for leg {missedLeg + 1}, but no verified correction is available; checking another return");
        }

        var backScope = $"{scope}-back";
        var label = $"Getting back onto the way up to {name}";
        var walks = 0;
        for (var leg = missedLeg - 1; leg > 0 && walks < MaxWalksBackOntoRoute; leg--)
        {
            if (CancelToken.IsCancellationRequested)
            {
                return NotOnRoute;
            }

            if (Svc.Objects.LocalPlayer is not { } player || !JumpFooting.CanWalkBetween(player.Position, route[leg].Point))
            {
                continue;
            }

            walks++;
            Status = label;
            Diag($"{backScope}: fell to {FormatPrecise(Svc.Objects.LocalPlayer?.Position ?? Vector3.Zero)}; walking to leg {leg + 1}/{route.Length} on this level");
            if (await RunLeg(JumpStep.Walk(route[leg].Point), route.Recorded, backScope))
            {
                return leg;
            }
        }

        var roofTakeoff = await ReturnToRoofTakeoff(route, missedLeg - 1, backScope);
        if (roofTakeoff != NotOnRoute)
        {
            return roofTakeoff;
        }

        if (CancelToken.IsCancellationRequested)
        {
            return NotOnRoute;
        }

        if (Svc.Objects.LocalPlayer is not { } strandedPlayer)
        {
            return NotOnRoute;
        }

        if (!JumpLanding.IsAtRouteGroundLevel(strandedPlayer.Position, route[0].Point))
        {
            return await RestartJumpRoute(route, name, backScope);
        }

        Diag($"{backScope}: at ground level; travelling back to the foot of the route");
        return await WalkToRouteStart(Svc.ClientState.TerritoryType, route[0].Point, name, backScope) ? 0 : NotOnRoute;
    }

    private async Task WalkStraightInto(VistaVolume volume, Vector3 logPoint, string scope)
    {
        if (IsStandingInside(volume))
        {
            return;
        }

        var navmesh = NavmeshIPC.Instance;
        var stall = new MoveStallTracker();
        var startedFrom = DistanceTo(logPoint);
        Status = "Walking to the log point";
        navmesh.MoveAlong([logPoint], false);
        await WaitUntilTimed(() => IsStandingInside(volume) || !navmesh.IsRunning() || stall.Check() != StallKind.None, JumpLegWatchdogMs, $"{scope}-top", 1);
        navmesh.Stop();
        Diag($"{scope}: walked straight at the log point from {startedFrom:F1}m out and ended {DescribeStanding(volume)}");
    }

    private static int NearestLegStoodOn(JumpRoute route, int lastAllowedLeg = int.MaxValue)
    {
        if (Svc.Objects.LocalPlayer is not { } player)
        {
            return NotOnRoute;
        }

        var nearest = JumpPosition.NearestLeg(route, player.Position, JumpPhysics.IsAirborne(), lastAllowedLeg);
        return nearest != NotOnRoute && JumpLanding.CanCorrect(player.Position, route[nearest].Point,
            route.Recorded, JumpFooting.InspectWalk(player.Position, route[nearest].Point)) ? nearest : NotOnRoute;
    }

    private static bool HasLandedOn(Vector3 point, float across)
        => Svc.Objects.LocalPlayer is { } player && !JumpPhysics.IsAirborne() && JumpPosition.OnPoint(player.Position, point, across);

    private static float HeightAbove(Vector3 point)
        => Svc.Objects.LocalPlayer is { } player ? player.Position.Y - point.Y : float.MinValue;
}
