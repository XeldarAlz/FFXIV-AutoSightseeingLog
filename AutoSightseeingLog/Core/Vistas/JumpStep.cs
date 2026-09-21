using System.Numerics;

namespace AutoSightseeingLog.Core.Vistas;

internal readonly record struct JumpStep(Vector3 Point, float RunUpSeconds)
{
    private const float WalkOnly = -1f;

    public bool Jumps => RunUpSeconds >= 0f;

    public static JumpStep Walk(Vector3 point) => new(point, WalkOnly);

    public static JumpStep Walk(float x, float y, float z) => new(new Vector3(x, y, z), WalkOnly);

    public static JumpStep Jump(float x, float y, float z, float runUpSeconds) => new(new Vector3(x, y, z), runUpSeconds);
}
