using AutoSightseeingLog.Core.Travel;
using AutoSightseeingLog.Core.Vistas;
using ECommons.DalamudServices;
using ECommons.MathHelpers;
using System.Diagnostics;
using System.Globalization;
using System.IO;
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
    private static double takeoffSpeed;
    private static float airborneApex;
    private static StreamWriter? trace;
    private static long traceStartedAt;
    private static uint traceTerritory;
    private static int traceSamples;

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
        if (!recording)
        {
            return;
        }

        if (Svc.Objects.LocalPlayer is not { } player || Svc.ClientState.TerritoryType != traceTerritory)
        {
            Stop();
            return;
        }

        var now = Stopwatch.GetTimestamp();
        var position = player.Position;
        var nowAirborne = JumpPhysics.IsAirborne();
        WriteSample(position, player.Rotation, nowAirborne, now);
        if (!airborne && nowAirborne)
        {
            TakeOff(player.Rotation);
        }
        else if (airborne && !nowAirborne)
        {
            Land(position, now);
        }
        else if (!nowAirborne)
        {
            TrackGround(position, now);
        }

        if (nowAirborne)
        {
            airborneApex = MathF.Max(airborneApex, position.Y);
        }

        airborne = nowAirborne;
        lastPosition = position;
        lastSampleAt = now;
    }

    private static void Start()
    {
        if (recording)
        {
            Svc.Chat.PrintError($"{AslConstants.LogPrefix} Already recording; /asl record stop finishes the current recording.");
            return;
        }

        if (Svc.Objects.LocalPlayer is not { } player)
        {
            Svc.Chat.PrintError($"{AslConstants.LogPrefix} Log in before recording.");
            return;
        }

        if (JumpPhysics.IsAirborne())
        {
            Svc.Chat.PrintError($"{AslConstants.LogPrefix} Stand on the starting platform before recording.");
            return;
        }

        var now = Stopwatch.GetTimestamp();
        traceStartedAt = now;
        traceTerritory = Svc.ClientState.TerritoryType;
        traceSamples = 0;
        try
        {
            var directory = Path.Combine(Svc.PluginInterface.GetPluginConfigDirectory(), "jump-recordings");
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, $"jump-{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}-{traceTerritory}.csv");
            trace = new StreamWriter(path);
            trace.WriteLine("seconds,x,y,z,rotation,airborne,walking");
            RunLog.Info($"record: full motion trace at {path}");
        }
        catch (Exception exception)
        {
            CloseTrace();
            RunLog.Warning($"record: could not create motion trace: {exception.Message}");
        }

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
        CloseTrace();
        RunLog.Info($"record: stopped with {steps.Count} steps");
        for (var index = 0; index < steps.Count; index++)
        {
            RunLog.Info($"record-route: {FormatStep(steps[index])}");
        }

        Svc.Chat.Print($"{AslConstants.LogPrefix} Recording stopped: {steps.Count} steps written to the plugin log.");
    }

    private static void TakeOff(float rotation)
    {
        // The first airborne sample already contains part of the flight. Keep the last grounded
        // position and speed separate so airborne travel is not encoded as a longer run-up.
        takeoff = lastPosition;
        airborneApex = takeoff.Y;
        takeoffSpeed = lastSpeed;
        takeoffFacingDegrees = rotation * DegreesPerRadian;
        var ranUp = moving && lastSpeed >= MovingMetersPerSecond;
        takeoffRunUpMeters = ranUp ? GroundDistance.Between(runStart, lastPosition) : 0f;
        takeoffRunUpSeconds = ranUp ? Stopwatch.GetElapsedTime(runStartAt, lastSampleAt).TotalSeconds : 0d;
        AddWalk(ranUp ? runStart : lastPosition, 0.01f);
    }

    private static void Land(Vector3 position, long now)
    {
        var runUpSeconds = takeoffSpeed < MovingMetersPerSecond ? 0f : JumpPhysics.RunUpSeconds(takeoffRunUpMeters);
        var jumpFrom = lastStep;
        var step = RecordedAirMotion.IsDrop(takeoff.Y, airborneApex, position.Y)
            ? JumpStep.Drop(position.X, position.Y, position.Z)
            : JumpStep.Jump(position.X, position.Y, position.Z, runUpSeconds);
        steps.Add(step);
        lastStep = position;
        runStart = position;
        runStartAt = now;
        lastMovedAt = now;
        lastSpeed = 0d;
        moving = false;
        RunLog.Info($"record#{steps.Count}: {FormatStep(step)} from {Format(jumpFrom)}, facing {takeoffFacingDegrees:F1} deg, ran {takeoffRunUpMeters:F3}m in {takeoffRunUpSeconds:F3}s, last ground speed {takeoffSpeed:F3}m/s at {Format(takeoff)}, {GroundDistance.Between(takeoff, position):F3}m across and {position.Y - takeoff.Y:+0.000;-0.000}m up in the air");
        Svc.Chat.Print($"{AslConstants.LogPrefix} {(step.Drops ? "Drop" : "Jump")} recorded (step {steps.Count}).");
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

    private static void AddWalk(Vector3 position, float tolerance = SameSpotMeters)
    {
        if (GroundDistance.Between(lastStep, position) < tolerance && MathF.Abs(lastStep.Y - position.Y) < tolerance)
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
        => step.Drops
            ? string.Create(CultureInfo.InvariantCulture, $"Drop({Coordinate(step.Point.X)}, {Coordinate(step.Point.Y)}, {Coordinate(step.Point.Z)}),")
            : step.Jumps
            ? string.Create(CultureInfo.InvariantCulture, $"Jump({Coordinate(step.Point.X)}, {Coordinate(step.Point.Y)}, {Coordinate(step.Point.Z)}, {Coordinate(step.RunUpSeconds)}),")
            : string.Create(CultureInfo.InvariantCulture, $"Walk({Coordinate(step.Point.X)}, {Coordinate(step.Point.Y)}, {Coordinate(step.Point.Z)}),");

    private static string Coordinate(float value)
        => string.Create(CultureInfo.InvariantCulture, $"{value:0.####}f");

    private static string Format(Vector3 position)
        => string.Create(CultureInfo.InvariantCulture, $"({position.X:F2}, {position.Y:F2}, {position.Z:F2})");

    private static void WriteSample(Vector3 position, float rotation, bool nowAirborne, long now)
    {
        if (trace is null)
        {
            return;
        }

        try
        {
            var seconds = Stopwatch.GetElapsedTime(traceStartedAt, now).TotalSeconds;
            trace.WriteLine(string.Create(CultureInfo.InvariantCulture,
                $"{seconds:F6},{position.X:F5},{position.Y:F5},{position.Z:F5},{rotation:F5},{nowAirborne},{Game.Ops.WalkModeOps.IsWalking()}"));
            if (++traceSamples % 120 == 0)
            {
                trace.Flush();
            }
        }
        catch (Exception exception)
        {
            CloseTrace();
            RunLog.Warning($"record: motion trace stopped: {exception.Message}");
        }
    }

    public static void Dispose()
    {
        recording = false;
        CloseTrace();
    }

    private static void CloseTrace()
    {
        try
        {
            trace?.Dispose();
        }
        catch (Exception exception)
        {
            RunLog.Warning($"record: closing motion trace failed: {exception.Message}");
        }
        finally
        {
            trace = null;
        }
    }
}
