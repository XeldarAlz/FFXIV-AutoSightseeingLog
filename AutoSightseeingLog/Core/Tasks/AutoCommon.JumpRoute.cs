using AutoSightseeingLog.Core.Game.Ops;
using AutoSightseeingLog.Core.Ipc;
using AutoSightseeingLog.Core.Travel;
using AutoSightseeingLog.Core.Vistas;
using Dalamud.Game.ClientState.Conditions;
using ECommons.DalamudServices;
using ECommons.MathHelpers;
using System.Diagnostics;
using System.Numerics;
using System.Threading.Tasks;

namespace AutoSightseeingLog.Core.Tasks;

internal abstract partial class AutoCommon
{
    private const int NotOnRoute = -1;
    private const int MaxRouteMisses = 8;
    private const int MaxMissesOnOneLeg = 3;
    private const int MaxRunUpRetries = 2;
    private const float RunUpStartLatencySeconds = 0.05f;
    private const float RunUpMetersPerSecond = 3.7f;
    private const float RunUpSlackMeters = 0.15f;
    private const int MaxWalksBackOntoRoute = 3;
    private const int JumpLegWatchdogMs = 20_000;
    private const int JumpLegSettleMs = 500;
    private const float LandedAcrossMeters = 1f;
    private const float LandedBelowMeters = 0.6f;
    private const float OnRouteAcrossMeters = 2f;
    private const float OnRouteRiseMeters = 0.5f;
    private const float SameLevelMeters = 1.5f;

    private async Task<bool> ReachByJumpRoute(Vista vista, VistaVolume volume, JumpStep[] route, string name, string scope)
    {
        var inZone = Svc.ClientState.TerritoryType == vista.TerritoryId;
        if (inZone && IsStandingInside(volume))
        {
            Diag($"{scope}: already {DescribeStanding(volume)}");
            return true;
        }

        var startLeg = inZone ? NearestLegStoodOn(route) : NotOnRoute;
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

    private async Task<bool> ClimbJumpRoute(JumpStep[] route, int startLeg, string name, string scope)
    {
        var navmesh = NavmeshIPC.Instance;
        var tolerance = navmesh.Tolerance();
        var wasWalking = WalkModeOps.IsWalking();
        navmesh.SetTolerance(NavmeshIPC.DefaultToleranceMeters);
        WalkModeOps.SetWalking(false);
        try
        {
            var leg = startLeg;
            var lastMissedLeg = NotOnRoute;
            var missesOnThatLeg = 0;
            for (var misses = 0; misses < MaxRouteMisses; misses++)
            {
                Status = $"Climbing to {name}";
                Diag($"{scope}: climbing from leg {leg + 1}/{route.Length} after {misses} miss(es)");
                var missedLeg = await RunLegs(route, leg, scope);
                if (missedLeg == route.Length)
                {
                    Diag($"{scope}: at the top of the jump route");
                    return true;
                }

                if (CancelToken.IsCancellationRequested)
                {
                    return false;
                }

                missesOnThatLeg = missedLeg == lastMissedLeg ? missesOnThatLeg + 1 : 1;
                lastMissedLeg = missedLeg;
                if (missesOnThatLeg >= MaxMissesOnOneLeg)
                {
                    Warn($"{scope}: missed leg {missedLeg + 1}/{route.Length} of the jump route to {name} {missesOnThatLeg} times in a row; giving up");
                    return false;
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

    private async Task<int> RunLegs(JumpStep[] route, int startLeg, string scope)
    {
        for (var leg = startLeg; leg < route.Length; leg++)
        {
            if (CancelToken.IsCancellationRequested)
            {
                return leg;
            }

            var step = leg == startLeg ? JumpStep.Walk(route[leg].Point) : route[leg];
            if (!await RunLeg(step, $"{scope}-leg{leg + 1}"))
            {
                return leg;
            }
        }

        return route.Length;
    }

    private async Task<bool> RunLeg(JumpStep step, string scope)
    {
        if (Svc.Objects.LocalPlayer is not { } player)
        {
            return false;
        }

        var navmesh = NavmeshIPC.Instance;
        var from = player.Position;
        var ranUp = 0f;
        for (var attempt = 0; ; attempt++)
        {
            var attemptFrom = player.Position;
            var startedAt = Stopwatch.GetTimestamp();
            navmesh.MoveAlong([step.Point], false);
            if (!step.Jumps)
            {
                break;
            }

            ranUp = await RunUp(step.RunUpSeconds, startedAt, attemptFrom);
            if (CancelToken.IsCancellationRequested)
            {
                navmesh.Stop();
                return false;
            }

            if (ranUp <= MaxRunUpMeters(step.RunUpSeconds) || attempt == MaxRunUpRetries)
            {
                if (UseGeneralAction(JumpGeneralActionId))
                {
                    break;
                }

                navmesh.Stop();
                Diag($"{scope}: the game refused the jump to {FormatPrecise(step.Point)} ({ConditionTag()})");
                return false;
            }

            navmesh.Stop();
            Diag($"{scope}: ran {ranUp:F2}m in the {step.RunUpSeconds:F2}s run-up, too far to jump from; stepping back to {FormatPrecise(from)}");
            await StepBackTo(from, step.Point, scope);
        }

        var stall = new MoveStallTracker();
        await WaitUntilTimed(() => (!navmesh.IsRunning() && !IsAirborne()) || stall.Check() != StallKind.None, JumpLegWatchdogMs, scope, 1);
        navmesh.Stop();
        await DelayMs(JumpLegSettleMs);

        var landed = HasLandedOn(step.Point);
        Diag($"{scope}: {(step.Jumps ? $"jump after a {step.RunUpSeconds:F2}s, {ranUp:F2}m run-up" : "walk")} to {FormatPrecise(step.Point)} {(landed ? "landed" : "MISSED")} {GroundDistanceTo(step.Point):F2}m across and {HeightAbove(step.Point):+0.00;-0.00}m high");
        return landed;
    }

    private async Task<float> RunUp(float runUpSeconds, long startedAt, Vector3 from)
    {
        while (Stopwatch.GetElapsedTime(startedAt).TotalSeconds < runUpSeconds)
        {
            if (CancelToken.IsCancellationRequested)
            {
                return 0f;
            }

            await NextFrame(1);
        }

        return DistanceTo(from);
    }

    private async Task StepBackTo(Vector3 from, Vector3 target, string scope)
    {
        var navmesh = NavmeshIPC.Instance;
        var away = new Vector3(from.X - target.X, 0f, from.Z - target.Z);
        var back = away == Vector3.Zero ? from : from + Vector3.Normalize(away) * NavmeshIPC.DefaultToleranceMeters;
        var stall = new MoveStallTracker();
        navmesh.MoveAlong([back], false);
        await WaitUntilTimed(() => !navmesh.IsRunning() || stall.Check() != StallKind.None, JumpLegWatchdogMs, $"{scope}-stepback", 1);
        navmesh.Stop();
        await DelayMs(JumpLegSettleMs);
    }

    private static float MaxRunUpMeters(float runUpSeconds)
        => MathF.Max(0f, (runUpSeconds - RunUpStartLatencySeconds) * RunUpMetersPerSecond) + RunUpSlackMeters;

    private async Task<int> FindWayBackOnto(JumpStep[] route, int missedLeg, string name, string scope)
    {
        var stoodOn = NearestLegStoodOn(route);
        if (stoodOn != NotOnRoute)
        {
            Diag($"{scope}: still on the route at leg {stoodOn + 1}/{route.Length}");
            return stoodOn;
        }

        var backScope = $"{scope}-back";
        var label = $"Getting back onto the way up to {name}";
        var nextLeg = missedLeg + 1;
        if (nextLeg < route.Length && OvershotJump(route, missedLeg) && IsWalkOnThisLevel(route[nextLeg]))
        {
            Status = label;
            Diag($"{backScope}: overshot to {FormatPrecise(Svc.Objects.LocalPlayer?.Position ?? Vector3.Zero)}; walking on to leg {nextLeg + 1}/{route.Length} on this level");
            if (await RunLeg(JumpStep.Walk(route[nextLeg].Point), backScope))
            {
                return nextLeg;
            }
        }

        var walks = 0;
        for (var leg = missedLeg - 1; leg > 0 && walks < MaxWalksBackOntoRoute; leg--)
        {
            if (CancelToken.IsCancellationRequested)
            {
                return NotOnRoute;
            }

            if (!IsWalkOnThisLevel(route[leg]))
            {
                continue;
            }

            walks++;
            Status = label;
            Diag($"{backScope}: fell to {FormatPrecise(Svc.Objects.LocalPlayer?.Position ?? Vector3.Zero)}; walking to leg {leg + 1}/{route.Length} on this level");
            if (await RunLeg(JumpStep.Walk(route[leg].Point), backScope))
            {
                return leg;
            }
        }

        Diag($"{backScope}: walking back to the foot of the route");
        return await WalkUpTo(route[0].Point, backScope, name) ? 0 : NotOnRoute;
    }

    private static bool IsWalkOnThisLevel(JumpStep step)
        => !step.Jumps && MathF.Abs(HeightAbove(step.Point)) <= SameLevelMeters;

    private static bool OvershotJump(JumpStep[] route, int leg)
    {
        if (leg == 0 || !route[leg].Jumps || Svc.Objects.LocalPlayer is not { } player)
        {
            return false;
        }

        var from = route[leg - 1].Point;
        var along = (route[leg].Point - from).ToVector2();
        var travelled = (player.Position - from).ToVector2();
        return Vector2.Dot(travelled, along) > along.LengthSquared();
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

    private static int NearestLegStoodOn(JumpStep[] route)
    {
        if (Svc.Objects.LocalPlayer is not { } player)
        {
            return NotOnRoute;
        }

        var position = player.Position;
        var nearestLeg = NotOnRoute;
        var nearestDistance = float.MaxValue;
        for (var leg = 0; leg < route.Length; leg++)
        {
            var point = route[leg].Point;
            if (GroundDistance.Between(position, point) > OnRouteAcrossMeters || MathF.Abs(position.Y - point.Y) > OnRouteRiseMeters)
            {
                continue;
            }

            var distance = Vector3.Distance(position, point);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestLeg = leg;
            }
        }

        return nearestLeg;
    }

    private static bool HasLandedOn(Vector3 point)
        => (GroundDistanceTo(point) <= LandedAcrossMeters && HeightAbove(point) >= -LandedBelowMeters) || IsOnLevelWith(point);

    private static bool IsOnLevelWith(Vector3 point)
        => GroundDistanceTo(point) <= OnRouteAcrossMeters && MathF.Abs(HeightAbove(point)) <= OnRouteRiseMeters;

    private static float HeightAbove(Vector3 point)
        => Svc.Objects.LocalPlayer is { } player ? player.Position.Y - point.Y : float.MinValue;

    private static bool IsAirborne()
        => Svc.Condition[ConditionFlag.Jumping] || Svc.Condition[ConditionFlag.Jumping61];
}
