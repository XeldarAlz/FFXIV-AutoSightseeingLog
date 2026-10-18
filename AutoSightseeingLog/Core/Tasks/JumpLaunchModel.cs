namespace AutoSightseeingLog.Core.Tasks;

internal readonly record struct JumpLaunchObservation(float RunUp, float Speed, float StartupTravel);

// A straight interpolation through a speed-capped sample underestimates intermediate jumps.
// Fit acceleration below that cap, then bound speed separately. Refit only after a flight.
internal sealed class JumpLaunchModel
{
    private readonly float minimumRunUp;
    private readonly float maximumRunUp;
    private readonly Line speed;
    private readonly Line startup;
    private readonly float speedCap = float.PositiveInfinity;
    private readonly bool ready;

    public JumpLaunchModel(IEnumerable<JumpLaunchObservation> observations)
    {
        var points = observations.GroupBy(value => MathF.Round(value.RunUp / 0.02f))
            .Select(group => new JumpLaunchObservation(Median(group.Select(value => value.RunUp)),
                Median(group.Select(value => value.Speed)), Median(group.Select(value => value.StartupTravel))))
            .OrderBy(value => value.RunUp).ToArray();
        if (points.Length < 2)
        {
            return;
        }

        minimumRunUp = points[0].RunUp;
        maximumRunUp = points[^1].RunUp;
        var maximumSpeed = points.Max(value => value.Speed);
        var accelerating = points.Where(value => value.Speed < maximumSpeed - 0.25f).ToArray();
        if (!TryFit(accelerating, value => value.Speed, out speed)
            && !TryFit(points, value => value.Speed, out speed))
        {
            return;
        }

        // Only infer a cap when the fastest observed jump is slower than the acceleration fit.
        // Otherwise the fastest observation is still accelerating and cannot establish a ceiling.
        if (speed.At(maximumRunUp) > points[^1].Speed + 0.25f)
        {
            speedCap = maximumSpeed;
        }

        ready = TryFit(points, value => value.StartupTravel, out startup);
    }

    public bool TryPredict(float runUp, out float airSpeed, out float startupTravel)
    {
        airSpeed = startupTravel = 0f;
        if (!ready || runUp < 0f || runUp < minimumRunUp - 0.1f || runUp > maximumRunUp + 0.15f)
        {
            return false;
        }

        airSpeed = MathF.Min(speedCap, speed.At(runUp));
        startupTravel = MathF.Max(0f, startup.At(runUp));
        return airSpeed > 0f && float.IsFinite(airSpeed) && float.IsFinite(startupTravel);
    }

    private static bool TryFit(JumpLaunchObservation[] points, Func<JumpLaunchObservation, float> value, out Line line)
    {
        var slopes = new List<float>();
        for (var first = 0; first < points.Length; first++)
        {
            for (var second = first + 1; second < points.Length; second++)
            {
                if (points[second].RunUp - points[first].RunUp >= 0.03f)
                {
                    slopes.Add((value(points[second]) - value(points[first]))
                        / (MathF.Sqrt(points[second].RunUp) - MathF.Sqrt(points[first].RunUp)));
                }
            }
        }

        line = default;
        if (slopes.Count == 0)
        {
            return false;
        }

        // Startup travel can be constant (or differ only by fitting noise). A tiny negative
        // slope must not discard an otherwise usable model, especially with just two samples.
        var slope = MathF.Max(0f, Median(slopes));
        line = new(slope, Median(points.Select(point => value(point) - slope * MathF.Sqrt(point.RunUp))));
        return true;
    }

    private readonly record struct Line(float Slope, float Intercept)
    {
        public float At(float runUp) => Intercept + Slope * MathF.Sqrt(runUp);
    }

    private static float Median(IEnumerable<float> values)
    {
        var ordered = values.Order().ToArray();
        var middle = ordered.Length / 2;
        return ordered.Length % 2 == 0 ? (ordered[middle - 1] + ordered[middle]) * 0.5f : ordered[middle];
    }
}
