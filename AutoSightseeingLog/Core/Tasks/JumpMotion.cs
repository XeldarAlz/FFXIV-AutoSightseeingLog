using System.Numerics;

namespace AutoSightseeingLog.Core.Tasks;

// These decisions use observations, not navigator completion: a ground path can finish in midair.
internal readonly record struct JumpSample(Vector3 Position, bool Airborne, double Seconds);

internal sealed class JumpVelocity(JumpSample initial)
{
    private JumpSample previous = initial;
    private float previousAverage;
    private double previousDuration;

    public float Speed { get; private set; }
    public float NextSpeed { get; private set; }

    public void Add(JumpSample sample)
    {
        var duration = sample.Seconds - previous.Seconds;
        if (duration <= 0d)
        {
            return;
        }

        var distance = Vector2.Distance(new(sample.Position.X, sample.Position.Z), new(previous.Position.X, previous.Position.Z));
        var average = distance / (float)duration;
        // Displacement / dt is the interval's average velocity, not velocity at its end.
        var acceleration = previousDuration > 0d ? (average - previousAverage) / (float)((duration + previousDuration) / 2d) : 0f;
        Speed = MathF.Max(0f, average + acceleration * (float)duration / 2f);
        NextSpeed = MathF.Max(0f, Speed + acceleration * (float)duration);
        previous = sample;
        previousAverage = average;
        previousDuration = duration;
    }
}

internal sealed class JumpGrounding
{
    private JumpSample? anchor;

    public bool Check(JumpSample sample)
    {
        if (sample.Airborne || anchor is not { } previous || Vector3.Distance(previous.Position, sample.Position) > 0.02f)
        {
            anchor = sample.Airborne ? null : sample;
            return false;
        }

        return sample.Seconds - previous.Seconds >= 0.15;
    }
}

internal enum RunUpDecision { Continue, Jump, Fell, Overshot, TimedOut, OffLine, NoLaunchWindow }

internal sealed class JumpRunUp(Vector3 from, Vector3 target, float meters, double startedAt, float slope = 0f)
{
    public const float SlackMeters = 0.08f;
    private readonly Vector2 direction = Direction(from, target);

    public RunUpDecision Check(JumpSample sample, float? predictedRange = null, float? nextRange = null, float? safeMeters = null, float nextTravel = 0f)
    {
        if (sample.Airborne)
        {
            return RunUpDecision.Fell;
        }

        var offset = new Vector2(sample.Position.X - from.X, sample.Position.Z - from.Z);
        var along = Vector2.Dot(offset, direction);
        // A recorded roof approach may descend while still grounded. Compare against that
        // measured slope, retaining the same fall margin and the existing distance/line limits.
        var floorHeight = from.Y + along * slope;
        if (sample.Position.Y < floorHeight - 0.15f || slope != 0f && sample.Position.Y > floorHeight + 0.15f)
        {
            return RunUpDecision.Fell;
        }

        var beside = MathF.Abs(offset.X * direction.Y - offset.Y * direction.X);
        if (beside > SlackMeters)
        {
            return RunUpDecision.OffLine;
        }

        var limit = predictedRange.HasValue ? safeMeters ?? meters + SlackMeters : MathF.Min(safeMeters ?? float.MaxValue, meters + SlackMeters);
        if (along > limit)
        {
            return RunUpDecision.Overshot;
        }

        if (predictedRange is { } range)
        {
            var remaining = Vector2.Distance(new(sample.Position.X, sample.Position.Z), new(target.X, target.Z));
            var error = range - remaining;
            if (error > 0.25f)
            {
                return RunUpDecision.Overshot;
            }

            // Choose the nearer of this launch frame and the estimated next one. Waiting for the
            // threshold to be crossed always adds a frame of acceleration, especially at low FPS.
            if (MathF.Abs(error) <= 0.25f && (error >= 0f || nextRange is { } next && MathF.Abs(error) <= MathF.Abs(next - remaining)))
            {
                return RunUpDecision.Jump;
            }

            if (along >= limit - 0.02f)
            {
                return RunUpDecision.NoLaunchWindow;
            }
        }
        else if (along >= meters || along > 0f && nextTravel > 0f && along + nextTravel >= meters && meters - along <= SlackMeters
            && MathF.Abs(along - meters) <= MathF.Abs(along + nextTravel - meters))
        {
            return RunUpDecision.Jump;
        }

        return sample.Seconds - startedAt >= 1.5 ? RunUpDecision.TimedOut : RunUpDecision.Continue;
    }

    private static Vector2 Direction(Vector3 from, Vector3 target)
    {
        var line = new Vector2(target.X - from.X, target.Z - from.Z);
        return line.LengthSquared() > 0f ? Vector2.Normalize(line) : Vector2.Zero;
    }
}

internal enum FlightDecision { WaitingForTakeoff, Flying, Touchdown, Missed, NeverTookOff, TimedOut }

internal sealed class JumpFlight(Vector3 from, Vector3 target, double startedAt, bool allowDescendingStartup = false)
{
    public Vector3? Takeoff { get; private set; }
    public Vector3? Touchdown { get; private set; }
    public float Apex { get; private set; } = from.Y;
    public bool RoseFromLaunch => Apex > from.Y + 0.1f;

    public FlightDecision Check(JumpSample sample)
    {
        Apex = MathF.Max(Apex, sample.Position.Y);
        // The jump flag precedes physical takeoff. On a recorded downhill launch the feet can
        // still descend during those startup frames. This short allowance ends on ascent, a
        // larger drop or timeout; it does not excuse a descending miss after the jump's apex.
        var descendingStartup = allowDescendingStartup && sample.Seconds - startedAt <= 0.25
            && sample.Position.Y >= from.Y - 0.4f && Apex <= from.Y + 0.1f;
        if (sample.Airborne)
        {
            Takeoff ??= sample.Position;
        }

        if (Takeoff is null)
        {
            if (!descendingStartup && sample.Position.Y < from.Y - 0.15f)
            {
                return FlightDecision.Missed;
            }

            return sample.Seconds - startedAt >= 0.75 ? FlightDecision.NeverTookOff : FlightDecision.WaitingForTakeoff;
        }

        if (!sample.Airborne)
        {
            Touchdown ??= sample.Position;
            return FlightDecision.Touchdown;
        }

        // Below the landing and descending: do not keep driving into the wall or across a lower roof.
        if (!descendingStartup && sample.Position.Y < target.Y - 0.6f && Apex > sample.Position.Y + 0.1f)
        {
            return FlightDecision.Missed;
        }

        return sample.Seconds - startedAt >= 8 ? FlightDecision.TimedOut : FlightDecision.Flying;
    }
}

internal static class JumpPosition
{
    public const float AlignMeters = 0.12f;

    public static bool NeedsLandingAlignment(Vector3 position, Vector3 target, bool recorded, Vistas.JumpStep? nextStep,
        float tolerance = AlignMeters)
        => recorded && !OnPoint(position, target, tolerance)
        // A recorded walk already positions the character for the next takeoff. Do not insist on
        // visiting an intermediate landing coordinate when we have settled on its ledge.
        && !(tolerance >= AlignMeters && nextStep is { Jumps: false, Drops: false } && OnPoint(position, target, 0.5f));

    public static bool OnPoint(Vector3 position, Vector3 target, float across, float height = 0.3f)
        => Vector2.Distance(new(position.X, position.Z), new(target.X, target.Z)) <= across
        && MathF.Abs(position.Y - target.Y) <= height;

    // A hop onto a slope can settle beyond its recorded touchdown at one of the following walk
    // points. Recognize that specific foothold; never skip another jump or loosen height checks.
    public static int FollowingWalkLeg(Vistas.JumpRoute route, int jumpedLeg, Vector3 position, bool airborne)
    {
        if (airborne || jumpedLeg < 0 || jumpedLeg >= route.Length
            || !route[jumpedLeg].Jumps && !route[jumpedLeg].Drops)
        {
            return -1;
        }

        var nearest = -1;
        var distance = float.MaxValue;
        for (var leg = jumpedLeg + 1; leg < route.Length && !route[leg].Jumps && !route[leg].Drops; leg++)
        {
            var point = route[leg].Point;
            var candidate = Vector3.DistanceSquared(position, point);
            if (candidate < distance && OnPoint(position, point, 0.35f))
            {
                nearest = leg;
                distance = candidate;
            }
        }

        return nearest;
    }

    // Never resume at an unvisited leg just because its ledge happens to be nearby.
    public static int NearestLeg(Vistas.JumpRoute route, Vector3 position, bool airborne, int lastAllowedLeg)
    {
        if (airborne)
        {
            return -1;
        }

        var nearest = -1;
        var distance = float.MaxValue;
        for (var leg = 0; leg <= Math.Min(lastAllowedLeg, route.Length - 1); leg++)
        {
            var point = route[leg].Point;
            if (!OnPoint(position, point, route.Recorded ? 0.35f : 0.5f))
            {
                continue;
            }

            var candidate = Vector3.DistanceSquared(position, point);
            if (candidate < distance)
            {
                nearest = leg;
                distance = candidate;
            }
        }

        return nearest;
    }
}

internal static class JumpTrace
{
    public static IEnumerable<JumpSample> Summarize(IReadOnlyList<JumpSample> samples)
    {
        var count = Math.Min(25, samples.Count);
        for (var index = 0; index < count; index++)
        {
            yield return samples[index * (samples.Count - 1) / Math.Max(1, count - 1)];
        }
    }
}
