using System.Numerics;

namespace AutoSightseeingLog.Core.Vistas;

internal static class JumpSurface
{
    public static bool IsWalkable(Vector3 v1, Vector3 v2, Vector3 v3, Vector3 colliderNormal)
    {
        var normal = Vector3.Cross(v2 - v1, v3 - v1);
        // Mesh hits supply triangle vertices; primitive colliders can supply only a shape normal.
        if (normal.LengthSquared() < 0.000001f)
        {
            normal = colliderNormal;
        }

        return normal.LengthSquared() > 0.000001f && MathF.Abs(Vector3.Normalize(normal).Y) >= 0.6f;
    }
}
