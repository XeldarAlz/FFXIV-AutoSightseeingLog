using System.Numerics;

namespace AutoSightseeingLog.Core.Vistas;

internal readonly record struct VistaVolume(Vector3 Center, Vector3 HalfExtents, float YawCosine, float YawSine, bool IsCylinder)
{
    public float Top => Center.Y + HalfExtents.Y;

    public float Bottom => Center.Y - HalfExtents.Y;

    public float NarrowestHalfWidth => MathF.Min(HalfExtents.X, HalfExtents.Z);

    public static VistaVolume Create(Vector3 center, Vector3 halfExtents, float yaw, bool isCylinder)
        => new(center, halfExtents, MathF.Cos(yaw), MathF.Sin(yaw), isCylinder);

    public Vector3 ToLocal(Vector3 world)
    {
        var offset = world - Center;
        return new Vector3(offset.X * YawCosine - offset.Z * YawSine, offset.Y, offset.X * YawSine + offset.Z * YawCosine);
    }

    public Vector3 ToWorld(Vector3 local)
        => new(Center.X + local.X * YawCosine + local.Z * YawSine, Center.Y + local.Y, Center.Z - local.X * YawSine + local.Z * YawCosine);

    public float HorizontalMargin(Vector3 world)
    {
        var local = ToLocal(world);
        if (!IsCylinder)
        {
            return MathF.Min(HalfExtents.X - MathF.Abs(local.X), HalfExtents.Z - MathF.Abs(local.Z));
        }

        var normalizedX = local.X / HalfExtents.X;
        var normalizedZ = local.Z / HalfExtents.Z;
        return (1f - MathF.Sqrt(normalizedX * normalizedX + normalizedZ * normalizedZ)) * NarrowestHalfWidth;
    }

    public float VerticalMargin(Vector3 world) => HalfExtents.Y - MathF.Abs(world.Y - Center.Y);

    public bool Contains(Vector3 world) => HorizontalMargin(world) >= 0f && VerticalMargin(world) >= 0f;
}
