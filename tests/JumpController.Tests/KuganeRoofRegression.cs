using AutoSightseeingLog.Core.Tasks;
using System.Numerics;

internal static class KuganeRoofRegression
{
    public static readonly Vector3 Launch = new(-49.60f, 43.81f, -70.35f);
    public static readonly Vector3 Target = new(-53.04f, 45.31f, -70.22f);

    // All usable calibration observations BEFORE the failed 12:35:52 jump (2026-10-18).
    // The failed flight below is held out when checking the launch model's prediction.
    public static readonly JumpLaunchObservation[] Prior =
    [
        new(0.105f, 4.46f, 0.282f),
        new(0.154f, 4.96f, 0.313f),
        new(0.208f, 5.42f, 0.354f),
        new(0.620f, 7.04f, 0.515f),
        new(0.080f, 4.10f, 0.241f),
        new(0f, 2.07f, 0.093f),
        new(0.112f, 4.51f, 0.273f),
    ];

    public static readonly JumpSample[] Miss =
    [
        new(new(-49.64f, 43.81f, -70.35f), true, 0.009),
        new(new(-49.77f, 43.81f, -70.35f), true, 0.032),
        new(new(-49.95f, 43.81f, -70.34f), true, 0.062),
        new(new(-50.10f, 43.92f, -70.33f), true, 0.085),
        new(new(-50.30f, 44.21f, -70.33f), true, 0.114),
        new(new(-50.45f, 44.42f, -70.32f), true, 0.138),
        new(new(-50.66f, 44.67f, -70.31f), true, 0.170),
        new(new(-50.81f, 44.84f, -70.31f), true, 0.193),
        new(new(-51.04f, 45.05f, -70.30f), true, 0.227),
        new(new(-51.18f, 45.17f, -70.29f), true, 0.249),
        new(new(-51.40f, 45.32f, -70.28f), true, 0.282),
        new(new(-51.55f, 45.41f, -70.28f), true, 0.305),
        new(new(-51.75f, 45.50f, -70.27f), true, 0.334),
        new(new(-51.95f, 45.57f, -70.26f), true, 0.366),
        new(new(-52.10f, 45.60f, -70.26f), true, 0.388),
        new(new(-52.30f, 45.61f, -70.25f), true, 0.418),
        new(new(-52.45f, 45.61f, -70.24f), true, 0.442),
        new(new(-52.65f, 45.57f, -70.24f), true, 0.471),
        new(new(-52.80f, 45.53f, -70.23f), true, 0.495),
        new(new(-53.00f, 45.45f, -70.22f), true, 0.524),
        new(new(-53.15f, 45.37f, -70.22f), true, 0.547),
        new(new(-53.38f, 45.22f, -70.21f), true, 0.581),
        new(new(-53.52f, 45.11f, -70.20f), true, 0.604),
        new(new(-53.73f, 44.92f, -70.19f), true, 0.636),
        new(new(-53.94f, 44.71f, -70.19f), true, 0.667),
    ];
}
