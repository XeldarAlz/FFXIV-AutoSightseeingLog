using System.Numerics;

namespace AutoSightseeingLog.Core.Vistas;

internal readonly record struct JumpStep(Vector3 Point, float RunUpSeconds, bool PreserveRunUp = false, float RunUpSlope = 0f)
{
    private const float WalkOnly = -1f;
    private const float DropOnly = -2f;

    public bool Jumps => RunUpSeconds >= 0f;
    public bool Drops => RunUpSeconds == DropOnly;

    // Narrow chained takeoffs can require a tighter landing than ordinary route waypoints.
    public float LandingTolerance { get; init; } = 0.12f;

    public static JumpStep Walk(Vector3 point) => new(point, WalkOnly);

    public static JumpStep Walk(float x, float y, float z) => new(new Vector3(x, y, z), WalkOnly);

    public static JumpStep Jump(float x, float y, float z, float runUpSeconds, bool preserveRunUp = false, float runUpSlope = 0f)
        => new(new Vector3(x, y, z), runUpSeconds, preserveRunUp, runUpSlope);

    public static JumpStep Drop(float x, float y, float z) => new(new Vector3(x, y, z), DropOnly);
}
