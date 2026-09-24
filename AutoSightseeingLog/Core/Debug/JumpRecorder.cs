using AutoSightseeingLog.Core.Travel;
using AutoSightseeingLog.Core.Vistas;
using ECommons.DalamudServices;
using ECommons.MathHelpers;
using System.Diagnostics;
using System.Globalization;
using System.Numerics;

namespace AutoSightseeingLog.Core.Debug;

internal static class JumpRecorder
{
    private const string StartArgument = "start";
    private const string StopArgument = "stop";
    private const string Usage = AslConstants.LogPrefix + " Usage: /asl record start, or /asl record stop.";
    private const float MovingMetersPerSecond = 0.5f;
    private const double StandStillSeconds = 0.3;
    private const float SameSpotMeters = 0.15f;
    private const float WaypointMinimumMeters = 0.75f;
    private const float WaypointTurnDegrees = 25f;
    private const float StandingRunUpMeters = 0.05f;
    private const float DegreesPerRadian = 180f / MathF.PI;

    private static readonly List<JumpStep> steps = [];
    private static bool recording;
    private static bool airborne;
    private static bool moving;
    private static long lastSampleAt;
    private static long lastMovedAt;
    private static double lastSpeed;
    private static long runStartAt;
    private static Vector3 lastPosition;
    private static Vector3 lastStep;
    private static Vector3 runStart;
    private static Vector3 takeoff;
    private static float takeoffRunUpMeters;
    private static double takeoffRunUpSeconds;
    private static float takeoffFacingDegrees;

    public static void HandleCommand(string arguments)
    {
        if (arguments.Equals(StartArgument, StringComparison.OrdinalIgnoreCase))
        {
            Start();
            return;
        }

        if (arguments.Equals(StopArgument, StringComparison.OrdinalIgnoreCase))
        {
            Stop();
            return;
        }

        Svc.Chat.PrintError(Usage);
    }

    public static void Tick()
    {
        if (!recording || Svc.Objects.LocalPlayer is not { } player)
        {
            return;
        }

        var now = Stopwatch.GetTimestamp();
        var position = player.Position;
        var nowAirborne = JumpPhysics.IsAirborne();
        if (!airborne && nowAirborne)
        {
            TakeOff(position, player.Rotation, now);
        }
        else if (airborne && !nowAirborne)
        {
            Land(position, now);
        }
        else if (!nowAirborne)
        {
            TrackGround(position, now);
        }

        airborne = nowAirborne;
        lastPosition = position;
        lastSampleAt = now;
    }

    private static void Start()
    {
        if (Svc.Objects.LocalPlayer is not { } player)
        {
            Svc.Chat.PrintError($"{AslConstants.LogPrefix} Log in before recording.");
            return;
        }

        var now = Stopwatch.GetTimestamp();
        steps.Clear();
        recording = true;
        airborne = JumpPhysics.IsAirborne();
        moving = false;
        lastSampleAt = now;
        lastMovedAt = now;
        lastSpeed = 0d;
        runStartAt = now;
        lastPosition = player.Position;
        runStart = player.Position;
        lastStep = player.Position;
        steps.Add(JumpStep.Walk(player.Position));
        RunLog.Info($"record: started in territory {Svc.ClientState.TerritoryType} at {Format(player.Position)}");
        Svc.Chat.Print($"{AslConstants.LogPrefix} Recording. Walk and jump the route by hand, then /asl record stop.");
    }

    private static void Stop()
    {
        if (!recording)
        {
            Svc.Chat.PrintError($"{AslConstants.LogPrefix} Nothing is being recorded.");
            return;
        }

        recording = false;
        RunLog.Info($"record: stopped with {steps.Count} steps");
        for (var index = 0; index < steps.Count; index++)
        {
            RunLog.Info($"record-route: {FormatStep(steps[index])}");
        }

        Svc.Chat.Print($"{AslConstants.LogPrefix} Recording stopped: {steps.Count} steps written to the plugin log.");
    }

    private static void TakeOff(Vector3 position, float rotation, long now)
    {
        takeoff = position;
        takeoffFacingDegrees = rotation * DegreesPerRadian;
        var ranUp = moving && lastSpeed >= MovingMetersPerSecond;
        takeoffRunUpMeters = ranUp ? GroundDistance.Between(runStart, position) : 0f;
        takeoffRunUpSeconds = ranUp ? Stopwatch.GetElapsedTime(runStartAt, now).TotalSeconds : 0d;
        AddWalk(ranUp ? runStart : lastPosition);
    }

    private static void Land(Vector3 position, long now)
    {
        var runUpSeconds = takeoffRunUpMeters < StandingRunUpMeters ? 0f : JumpPhysics.RunUpSeconds(takeoffRunUpMeters);
        var jumpFrom = lastStep;
        var step = JumpStep.Jump(position.X, position.Y, position.Z, runUpSeconds);
        steps.Add(step);
        lastStep = position;
        runStart = position;
        runStartAt = now;
        lastMovedAt = now;
        lastSpeed = 0d;
        moving = false;
        RunLog.Info($"record#{steps.Count}: {FormatStep(step)} from {Format(jumpFrom)}, facing {takeoffFacingDegrees:F0} deg, ran {takeoffRunUpMeters:F2}m in {takeoffRunUpSeconds:F2}s, took off at {Format(takeoff)}, {GroundDistance.Between(takeoff, position):F2}m across and {position.Y - takeoff.Y:+0.00;-0.00}m up in the air");
        Svc.Chat.Print($"{AslConstants.LogPrefix} Jump recorded (step {steps.Count}).");
    }

    private static void TrackGround(Vector3 position, long now)
    {
        var seconds = Stopwatch.GetElapsedTime(lastSampleAt, now).TotalSeconds;
        var speed = seconds > 0d ? GroundDistance.Between(lastPosition, position) / seconds : 0d;
        lastSpeed = speed;
        if (speed < MovingMetersPerSecond)
        {
            if (Stopwatch.GetElapsedTime(lastMovedAt, now).TotalSeconds >= StandStillSeconds)
            {
                moving = false;
                AddWalk(position);
                runStart = position;
                runStartAt = now;
            }

            return;
        }

        lastMovedAt = now;

        if (!moving)
        {
            moving = true;
            runStart = lastPosition;
            runStartAt = lastSampleAt;
            return;
        }

        if (GroundDistance.Between(lastStep, lastPosition) < WaypointMinimumMeters || TurnDegrees(lastStep, lastPosition, position) < WaypointTurnDegrees)
        {
            return;
        }

        AddWalk(lastPosition);
        runStart = lastPosition;
        runStartAt = lastSampleAt;
    }

    private static void AddWalk(Vector3 position)
    {
        if (GroundDistance.Between(lastStep, position) < SameSpotMeters && MathF.Abs(lastStep.Y - position.Y) < SameSpotMeters)
        {
            return;
        }

        var step = JumpStep.Walk(position);
        steps.Add(step);
        lastStep = position;
        RunLog.Info($"record#{steps.Count}: {FormatStep(step)}");
    }

    private static float TurnDegrees(Vector3 from, Vector3 through, Vector3 to)
    {
        var heading = (through - from).ToVector2();
        var turn = (to - through).ToVector2();
        if (heading == Vector2.Zero || turn == Vector2.Zero)
        {
            return 0f;
        }

        var cosine = Vector2.Dot(Vector2.Normalize(heading), Vector2.Normalize(turn));
        return MathF.Acos(Math.Clamp(cosine, -1f, 1f)) * DegreesPerRadian;
    }

    private static string FormatStep(JumpStep step)
        => step.Jumps
            ? string.Create(CultureInfo.InvariantCulture, $"Jump({Coordinate(step.Point.X)}, {Coordinate(step.Point.Y)}, {Coordinate(step.Point.Z)}, {Coordinate(step.RunUpSeconds)}),")
            : string.Create(CultureInfo.InvariantCulture, $"Walk({Coordinate(step.Point.X)}, {Coordinate(step.Point.Y)}, {Coordinate(step.Point.Z)}),");

    private static string Coordinate(float value)
        => string.Create(CultureInfo.InvariantCulture, $"{value:0.##}f");

    private static string Format(Vector3 position)
        => string.Create(CultureInfo.InvariantCulture, $"({position.X:F2}, {position.Y:F2}, {position.Z:F2})");
}
