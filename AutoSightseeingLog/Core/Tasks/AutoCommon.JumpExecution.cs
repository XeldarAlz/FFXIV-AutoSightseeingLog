using AutoSightseeingLog.Core.Game.Ops;
using AutoSightseeingLog.Core.Ipc;
using AutoSightseeingLog.Core.Travel;
using AutoSightseeingLog.Core.Vistas;
using ECommons.DalamudServices;
using System.Diagnostics;
using System.Numerics;
using System.Threading.Tasks;
using ClientGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace AutoSightseeingLog.Core.Tasks;

internal abstract partial class AutoCommon
{
    private const int JumpLegWatchdogMs = 20_000;

    private static JumpSample SampleJump()
        => new(Svc.Objects.LocalPlayer?.Position ?? Vector3.Zero, JumpPhysics.IsAirborne(),
            Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency);

    private async Task<bool> WaitForJumpGrounding(string scope)
    {
        var grounding = new JumpGrounding();
        return await WaitUntilTimed(() => Svc.Objects.LocalPlayer is not null && grounding.Check(SampleJump()),
            8_000, $"{scope}-ground", 1);
    }

    private async Task<bool> RunLeg(JumpStep step, bool recorded, string scope, JumpTrajectory? trajectory = null, JumpStep? nextStep = null, bool correction = false)
    {
        var navmesh = NavmeshIPC.Instance;
        navmesh.Stop();
        try
        {
            if (!await WaitForJumpGrounding(scope) || CancelToken.IsCancellationRequested)
            {
                return false;
            }

            if (!step.Jumps && !step.Drops)
            {
                return await WalkJumpLeg(step.Point, recorded, scope, correction, step.LandingTolerance);
            }

            if (HasJumpSpeedBuff())
            {
                Status = "Waiting for Sprint or Peloton to expire before jumping";
                Diag($"{scope}: waiting for movement speed buff before measuring the launch");
                if (!await WaitUntilTimed(() => !HasJumpSpeedBuff(), 35_000, $"{scope}-pace", 1)
                    || !await WaitForJumpGrounding(scope))
                {
                    return false;
                }
            }

            return await ExecuteJump(step, recorded, trajectory ?? new JumpTrajectory(), nextStep, scope);
        }
        finally
        {
            navmesh.Stop();
        }
    }

    private static bool HasJumpSpeedBuff()
        => Svc.Objects.LocalPlayer?.StatusList.Any(status => status.StatusId is 50 or 1199) == true;

    private async Task<bool> WalkJumpLeg(Vector3 point, bool recorded, string scope, bool correction = false,
        float landingTolerance = JumpPosition.AlignMeters)
    {
        var navmesh = NavmeshIPC.Instance;
        var wasWalking = WalkModeOps.IsWalking();
        var navTolerance = navmesh.Tolerance();
        var stall = new MoveStallTracker();
        var guard = correction ? new JumpCorrectionGuard(SampleJump().Position, point) : null;
        var lostFooting = false;
        try
        {
            if (landingTolerance < JumpPosition.AlignMeters)
            {
                navmesh.SetTolerance(MathF.Min(navTolerance, landingTolerance * 0.75f));
            }
            // Set pace before enabling movement; otherwise even a short correction starts running.
            WalkModeOps.SetWalking(correction || GroundDistanceTo(point) < 0.75f);
            navmesh.MoveAlong([point], false);
            var completed = await WaitUntilTimed(() =>
            {
                // Approach narrow takeoff spots at walking pace, so a frame cannot carry us across them.
                WalkModeOps.SetWalking(correction || GroundDistanceTo(point) < 0.75f);
                lostFooting = guard?.LostFooting(SampleJump()) == true;
                return lostFooting || !navmesh.IsRunning() || stall.Check() != StallKind.None
                    || JumpPhysics.IsAirborne() && HeightAbove(point) < -0.6f;
            }, JumpLegWatchdogMs, scope, 1);
            navmesh.Stop();
            var grounded = await WaitForJumpGrounding(scope);
            var landed = completed && grounded && !lostFooting && !CancelToken.IsCancellationRequested
                && HasLandedOn(point, recorded ? landingTolerance : 0.5f);
            Diag($"{scope}: walk to {FormatPrecise(point)} {(landed ? "reached" : "MISSED")}, {GroundDistanceTo(point):F2}m across, {HeightAbove(point):+0.00;-0.00}m high");
            if (lostFooting)
            {
                Diag($"{scope}: correction stopped immediately after losing grounded footing");
            }
            return landed;
        }
        finally
        {
            navmesh.Stop();
            navmesh.SetTolerance(navTolerance);
            WalkModeOps.SetWalking(wasWalking);
        }
    }

    private async Task<bool> ExecuteJump(JumpStep step, bool recorded, JumpTrajectory trajectory, JumpStep? nextStep, string scope)
    {
        var navmesh = NavmeshIPC.Instance;
        FaceTowards(step.Point);
        await NextFrame(1);
        if (CancelToken.IsCancellationRequested || Svc.Objects.LocalPlayer is null || JumpPhysics.IsAirborne())
        {
            return false;
        }

        var from = SampleJump();
        var launch = from;
        var speed = 0f;
        var maxFrame = 0d;
        float? launchRange = null;
        var runUpMeters = JumpPhysics.LaunchDistance(step.RunUpSeconds);
        if (step.RunUpSeconds > 0f)
        {
            var room = JumpFooting.RunUpRoom(from.Position, step.Point, step.RunUpSlope);
            from = SampleJump();
            var runUp = new JumpRunUp(from.Position, step.Point, runUpMeters, from.Seconds, step.RunUpSlope);
            var previous = from;
            var velocity = new JumpVelocity(from);
            navmesh.MoveAlong([step.Point], false);
            while (!CancelToken.IsCancellationRequested && Svc.Objects.LocalPlayer is not null)
            {
                await NextFrame(1);
                launch = SampleJump();
                var elapsed = launch.Seconds - previous.Seconds;
                maxFrame = Math.Max(maxFrame, elapsed);
                velocity.Add(launch);
                speed = velocity.Speed;
                var nextTravel = (speed + velocity.NextSpeed) * 0.5f * (float)elapsed;
                var travelled = GroundDistance.Between(from.Position, launch.Position);
                float? range = null;
                float? nextRange = null;
                if (!step.PreserveRunUp && trajectory.TryRange(travelled, step.Point.Y - launch.Position.Y, out var predicted))
                {
                    range = predicted;
                    if (trajectory.TryRange(travelled + nextTravel, step.Point.Y - launch.Position.Y, out var next))
                    {
                        nextRange = next + nextTravel;
                    }
                }
                else if (!step.PreserveRunUp && !trajectory.CanReachHeight(step.Point.Y - launch.Position.Y))
                {
                    navmesh.Stop();
                    Diag($"{scope}: target is above the measured jump apex; no launch window from {FormatPrecise(launch.Position)}");
                    return false;
                }

                var decision = runUp.Check(launch, range, nextRange, room, nextTravel);
                if (decision == RunUpDecision.Jump)
                {
                    launchRange = range;
                    break;
                }

                if (decision != RunUpDecision.Continue || !navmesh.IsRunning())
                {
                    navmesh.Stop();
                    Diag($"{scope}: run-up rejected ({decision}), wanted {runUpMeters:F3}m, moved {GroundDistance.Between(from.Position, launch.Position):F3}m, speed {speed:F2}m/s, supported run-up {(room.HasValue ? $"{room:F2}m" : "unavailable; limited to recorded run-up")}, max frame {maxFrame * 1000:F0}ms, calibrated {trajectory.Count} arc(s)");
                    return false;
                }

                previous = launch;
            }
        }

        if (CancelToken.IsCancellationRequested || Svc.Objects.LocalPlayer is null || JumpPhysics.IsAirborne())
        {
            return false;
        }

        // Standing jumps are issued while navigation is stopped. Moving jumps retain their measured
        // launch speed. A successful action call only starts the takeoff watchdog, never proves a jump.
        if (step.Jumps && !UseGeneralAction(JumpGeneralActionId))
        {
            Diag($"{scope}: the game refused the jump ({ConditionTag()})");
            return false;
        }

        if (step.Drops)
        {
            Diag($"{scope}: walking off the recorded ledge to {FormatPrecise(step.Point)} without issuing Jump");
        }
        else if (step.PreserveRunUp)
        {
            Diag($"{scope}: preserving the recorded {runUpMeters:F3}m run-up for this obstructed transition");
            if (step.RunUpSlope != 0f)
            {
                Diag($"{scope}: following recorded run-up slope {step.RunUpSlope:F2}m vertically per metre forward");
            }
        }

        navmesh.MoveAlong([step.Point], false);
        var flight = new JumpFlight(launch.Position, step.Point, launch.Seconds, allowDescendingStartup: step.RunUpSlope < 0f);
        var samples = new List<JumpSample>();
        var result = FlightDecision.WaitingForTakeoff;
        var last = launch;
        while (!CancelToken.IsCancellationRequested && Svc.Objects.LocalPlayer is not null)
        {
            await NextFrame(1);
            var sample = SampleJump();
            maxFrame = Math.Max(maxFrame, sample.Seconds - last.Seconds);
            last = sample;
            result = flight.Check(sample);
            if (!step.Drops && result == FlightDecision.WaitingForTakeoff && sample.Seconds - launch.Seconds > 0.2)
            {
                navmesh.Stop();
            }

            if (sample.Airborne)
            {
                samples.Add(sample);
            }

            if (result is not (FlightDecision.WaitingForTakeoff or FlightDecision.Flying))
            {
                break;
            }
        }

        // Stop at the first contact, including an off-target contact, before a correction can walk off.
        navmesh.Stop();
        if (CancelToken.IsCancellationRequested)
        {
            return false;
        }

        var grounded = await WaitForJumpGrounding(scope);
        var arc = default(JumpArc);
        var fitted = step.Jumps && !step.PreserveRunUp
            && trajectory.Observe(samples, launch, GroundDistance.Between(from.Position, launch.Position), out arc);
        Diag($"{scope}: {result}; launch {FormatPrecise(launch.Position)}, run-up {GroundDistance.Between(from.Position, launch.Position):F3}/{runUpMeters:F3}m, speed {speed:F2}m/s, apex {flight.Apex:F2}, max frame {maxFrame * 1000:F0}ms; target {FormatPrecise(step.Point)}, ended {GroundDistanceTo(step.Point):F2}m across, {HeightAbove(step.Point):+0.00;-0.00}m high");
        if (launchRange is { } predictedRange)
        {
            Diag($"{scope}: predicted {predictedRange:F3}m including takeoff startup for {GroundDistance.Between(launch.Position, step.Point):F3}m remaining to target");
        }

        Diag($"{scope}: {(flight.Takeoff.HasValue ? $"observed airborne at {FormatPrecise(flight.Takeoff.Value)}" : "no observed takeoff")}, {(flight.Touchdown.HasValue ? $"first contact {FormatPrecise(flight.Touchdown.Value)}" : "no contact before stopping movement")}; {(fitted ? $"learned arc: vertical {arc.VerticalSpeed:F2}m/s, gravity {arc.Gravity:F2}m/s2, horizontal {arc.HorizontalSpeed:F2}m/s, fit error {arc.Error:F3}m" : "arc not used for calibration")}");
        if (fitted)
        {
            Diag($"{scope}: physical takeoff {(arc.TakeoffSeconds - launch.Seconds) * 1000:F0}ms after the action, {Vector2.Distance(new(launch.Position.X, launch.Position.Z), arc.TakeoffPosition):F3}m of startup travel; {trajectory.Count} distance/arc observation(s)");
        }

        var landed = result == FlightDecision.Touchdown && grounded && !CancelToken.IsCancellationRequested && HasLandedOn(step.Point, 1f);
        if (landed && Svc.Objects.LocalPlayer is { } landedPlayer && JumpPosition.NeedsLandingAlignment(landedPlayer.Position, step.Point, recorded, nextStep, step.LandingTolerance))
        {
            var floor = JumpFooting.InspectWalk(landedPlayer.Position, step.Point);
            if (!JumpLanding.CanCorrect(landedPlayer.Position, step.Point, recorded, floor))
            {
                Diag($"{scope}: landing correction blocked ({floor}); preserving the current foothold");
                landed = false;
            }
            else
            {
                Diag($"{scope}: aligning {GroundDistanceTo(step.Point):F2}m to the recorded takeoff at walking pace; floor {floor}");
                landed = await WalkJumpLeg(step.Point, true, $"{scope}-align", correction: true, step.LandingTolerance);
            }
        }
        else if (landed && recorded && !HasLandedOn(step.Point, JumpPosition.AlignMeters))
        {
            Diag($"{scope}: settled on the landing ledge; continuing with the recorded walk to the next takeoff");
        }

        if (!landed)
        {
            if (step.Jumps && !flight.RoseFromLaunch)
            {
                Diag($"{scope}: failed takeoff: no measured rise above 0.10m; inspect startup footing or collision before changing the airborne range");
            }

            // Keep a bounded trace of the actual motion for comparing FPS, collisions and takeoff timing.
            var trace = JumpTrace.Summarize(samples)
                .Select(sample => $"{sample.Seconds - launch.Seconds:F3}s:{FormatPrecise(sample.Position)}");
            Diag($"{scope}: MISSED; flight samples {string.Join("; ", trace)}");
        }

        return landed;
    }

    private static unsafe void FaceTowards(Vector3 point)
    {
        if (Svc.Objects.LocalPlayer is { } player)
        {
            var direction = point - player.Position;
            ((ClientGameObject*)player.Address)->SetRotation(MathF.Atan2(direction.X, direction.Z));
        }
    }
}
