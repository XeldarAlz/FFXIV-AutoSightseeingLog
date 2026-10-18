namespace AutoSightseeingLog.Core.Vistas;

internal static partial class JumpPhysics
{
    private const float RunUpStartLatencySeconds = 0.05f;
    private const float RunUpMetersPerSecond = 3.7f;

    public static float RunUpMeters(float runUpSeconds)
        => MathF.Max(0f, (runUpSeconds - RunUpStartLatencySeconds) * RunUpMetersPerSecond);

    public static float RunUpSeconds(float runUpMeters)
        => (runUpMeters / RunUpMetersPerSecond) + RunUpStartLatencySeconds;

    // Positive legacy timings below the fitted latency still mean a moving jump, not a standing one.
    public static float LaunchDistance(float runUpSeconds)
        => runUpSeconds > 0f ? MathF.Max(0.01f, RunUpMeters(runUpSeconds)) : 0f;
}
