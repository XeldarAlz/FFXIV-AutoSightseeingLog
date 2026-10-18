using System.Numerics;

namespace AutoSightseeingLog.Core.Tasks;

internal readonly record struct JumpArc(float VerticalSpeed, float Gravity, float HorizontalSpeed, float Error)
{
    public double TakeoffSeconds { get; init; }
    public Vector2 TakeoffPosition { get; init; }

    public bool TryFlightSeconds(float rise, out float seconds)
    {
        var discriminant = VerticalSpeed * VerticalSpeed - 2f * Gravity * rise;
        // Recorded heights and fitted apexes have centimetre-scale error. An apex landing must
        // not become "impossible" because those two measurements differ by a few millimetres.
        var reachable = Gravity > 0f && discriminant >= -2f * Gravity * 0.05f;
        seconds = reachable ? (VerticalSpeed + MathF.Sqrt(MathF.Max(0f, discriminant))) / Gravity : 0f;
        return reachable && seconds > 0f;
    }

    // Fit y = a + b*t + c*t*t. The free intercept removes action/condition notification latency
    // from the fit; deriving launch speed from the apex also avoids assuming an exact takeoff frame.
    public static bool TryFit(IReadOnlyList<JumpSample> samples, float launchHeight, out JumpArc arc)
    {
        arc = default;
        // The jump condition can turn on while the feet are still on the ground. Those frames are
        // action startup, not part of a parabola (the Kugane trace has roughly 80 ms of this).
        samples = samples.SkipWhile(sample => sample.Position.Y <= launchHeight + 0.05f).ToArray();
        if (samples.Count > 0)
        {
            // Jumping stays set during the landing animation, even after all movement has stopped.
            // Those grounded tail frames flatten both the vertical fit and the measured air speed.
            var end = samples.Count - 1;
            var belowApex = samples.Max(sample => sample.Position.Y) > samples[^1].Position.Y + 0.08f;
            while (end > 0 && (Vector3.Distance(samples[end - 1].Position, samples[^1].Position) <= 0.02f
                || belowApex && MathF.Abs(samples[end - 1].Position.Y - samples[^1].Position.Y) <= 0.02f))
            {
                end--;
            }

            if (samples[^1].Seconds - samples[end].Seconds >= 0.04)
            {
                samples = samples.Take(end).ToArray();
            }
        }

        if (samples.Count < 6 || samples[^1].Seconds - samples[0].Seconds < 0.2)
        {
            return false;
        }

        var sums = new double[5];
        var heights = new double[3];
        var start = samples[0].Seconds;
        foreach (var sample in samples)
        {
            var t = sample.Seconds - start;
            var power = 1d;
            for (var index = 0; index < sums.Length; index++)
            {
                sums[index] += power;
                if (index < heights.Length)
                {
                    heights[index] += sample.Position.Y * power;
                }

                power *= t;
            }
        }

        var matrix = new double[3, 4];
        for (var row = 0; row < 3; row++)
        {
            for (var column = 0; column < 3; column++)
            {
                matrix[row, column] = sums[row + column];
            }

            matrix[row, 3] = heights[row];
        }

        for (var column = 0; column < 3; column++)
        {
            var divisor = matrix[column, column];
            if (Math.Abs(divisor) < 1e-9)
            {
                return false;
            }

            for (var index = column; index < 4; index++)
            {
                matrix[column, index] /= divisor;
            }

            for (var row = 0; row < 3; row++)
            {
                if (row == column)
                {
                    continue;
                }

                var factor = matrix[row, column];
                for (var index = column; index < 4; index++)
                {
                    matrix[row, index] -= factor * matrix[column, index];
                }
            }
        }

        var a = matrix[0, 3];
        var b = matrix[1, 3];
        var c = matrix[2, 3];
        var gravity = -2d * c;
        var apexRise = a - b * b / (4d * c) - launchHeight;
        if (!double.IsFinite(apexRise) || gravity is < 1d or > 100d || apexRise is < 0.2d or > 5d)
        {
            return false;
        }

        var error = 0d;
        var first = samples[0];
        var last = samples[^1];
        var duration = last.Seconds - first.Seconds;
        var horizontal = new Vector2(last.Position.X - first.Position.X, last.Position.Z - first.Position.Z);
        foreach (var sample in samples)
        {
            var t = sample.Seconds - start;
            error = Math.Max(error, Math.Abs(sample.Position.Y - (a + b * t + c * t * t)));
            var expected = new Vector2(first.Position.X, first.Position.Z) + horizontal * (float)(t / duration);
            error = Math.Max(error, Vector2.Distance(expected, new(sample.Position.X, sample.Position.Z)));
        }

        // Collisions, steering and changing airborne speed invalidate a simple ballistic prediction.
        if (error > 0.12d)
        {
            return false;
        }

        var verticalSpeed = Math.Sqrt(2d * gravity * apexRise);
        var takeoffOffset = (verticalSpeed - b) / (2d * c);
        arc = new((float)verticalSpeed, (float)gravity,
            horizontal.Length() / (float)duration, (float)error)
        {
            TakeoffSeconds = start + takeoffOffset,
            TakeoffPosition = new Vector2(first.Position.X, first.Position.Z) + horizontal * (float)(takeoffOffset / duration),
        };
        return true;
    }
}

internal sealed class JumpTrajectory
{
    private readonly Queue<(JumpArc Arc, float RunUp, float StartupTravel)> observations = new();
    private JumpArc fittedArc;
    private JumpLaunchModel launchModel = new([]);

    public int Count => observations.Count;

    public bool Observe(IReadOnlyList<JumpSample> samples, JumpSample launch, float runUpMeters, out JumpArc arc)
    {
        if (!JumpArc.TryFit(samples, launch.Position.Y, out arc) || runUpMeters < 0f || arc.HorizontalSpeed < 0.2f)
        {
            return false;
        }

        var delay = arc.TakeoffSeconds - launch.Seconds;
        var startupTravel = Vector2.Distance(new(launch.Position.X, launch.Position.Z), arc.TakeoffPosition);
        if (delay is < -0.03 or > 0.25 || startupTravel > 2f)
        {
            return false;
        }

        observations.Enqueue((arc, runUpMeters, startupTravel));
        if (observations.Count > 12)
        {
            observations.Dequeue();
        }

        var values = observations.ToArray();
        fittedArc = new JumpArc(Median(values.Select(value => value.Arc.VerticalSpeed)),
            Median(values.Select(value => value.Arc.Gravity)), 0f, 0f);
        launchModel = new(observations.Select(value => new JumpLaunchObservation(value.RunUp,
            value.Arc.HorizontalSpeed, value.StartupTravel)));
        return true;
    }

    public bool CanReachHeight(float rise) => Count < 2 || fittedArc.TryFlightSeconds(rise, out _);

    // Every run-up starts from observed rest. Speed grows approximately with sqrt(distance),
    // and jumping adds an observed startup phase before the ballistic arc. Learn that mapping
    // directly instead of dividing air speed by a noisy single-frame ground-speed measurement.
    public bool TryRange(float runUpMeters, float rise, out float range)
    {
        range = 0f;
        if (observations.Count < 2 || runUpMeters < 0f)
        {
            return false;
        }

        if (!fittedArc.TryFlightSeconds(rise, out var seconds))
        {
            return false;
        }

        if (!launchModel.TryPredict(runUpMeters, out var airSpeed, out var startup))
        {
            return false;
        }
        range = startup + airSpeed * seconds;
        return airSpeed > 0f && float.IsFinite(range);
    }

    private static float Median(IEnumerable<float> values)
    {
        var ordered = values.Order().ToArray();
        return ordered[ordered.Length / 2];
    }
}
