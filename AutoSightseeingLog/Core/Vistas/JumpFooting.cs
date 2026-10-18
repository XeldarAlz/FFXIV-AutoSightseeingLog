using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;
using System.Numerics;

namespace AutoSightseeingLog.Core.Vistas;

internal static class JumpFooting
{
    public static bool TryRecoveryPath(Vector3 from, Vector3 to, out JumpRecoveryPath? path, out JumpRecoveryFailure failure)
        => JumpRecoveryPath.TryCreate(from, to, RecoveryFloor, RecoveryClear, out path, out failure);

    private static Vector3? RecoveryFloor(Vector3 expected)
    {
        // Search only near the last supported height, never down to a different roof or the street.
        return BGCollisionModule.RaycastMaterialFilter(expected + Vector3.UnitY * 0.4f, -Vector3.UnitY, out var hit, 0.8f)
            && JumpSurface.IsWalkable(hit.V1, hit.V2, hit.V3, hit.Normal) ? hit.Point : null;
    }

    private static bool RecoveryClear(Vector3 from, Vector3 to)
    {
        var line = to - from;
        var length = line.Length();
        if (length < 0.001f)
        {
            return true;
        }

        return !BGCollisionModule.RaycastMaterialFilter(from + Vector3.UnitY * 0.4f, line / length, out _, length)
            && !BGCollisionModule.RaycastMaterialFilter(from + Vector3.UnitY, line / length, out _, length);
    }

    public static float? RunUpRoom(Vector3 from, Vector3 target, float slope = 0f)
    {
        // The caller has already observed stable grounded footing here. If this ray cannot see
        // that floor, it cannot prove there is zero runway either; keep the recorded distance cap.
        if (!HasFloor(from))
        {
            return null;
        }

        var line = new Vector3(target.X - from.X, 0f, target.Z - from.Z);
        var length = line.Length();
        if (length < 0.05f)
        {
            return 0f;
        }

        var direction = line / length;
        var available = 0f;
        var previous = from;
        for (var distance = 0.05f; distance <= MathF.Min(2f, length - 0.1f); distance += 0.05f)
        {
            var point = from + direction * distance + Vector3.UnitY * (distance * slope);
            var segment = point - previous;
            if (!HasFloor(point) || BGCollisionModule.RaycastMaterialFilter(
                previous + Vector3.UnitY * 0.5f, Vector3.Normalize(segment), out _, segment.Length()))
            {
                break;
            }

            available = distance;
            previous = point;
        }

        return MathF.Max(0f, available - 0.05f);
    }

    public static bool CanWalkBetween(Vector3 from, Vector3 to)
        => InspectWalk(from, to) == JumpFloorPath.Supported;

    // A ray which cannot see the floor under a grounded player cannot establish a gap ahead.
    // Keep that case separate from a visible edge or obstacle, including for tiny corrections.
    public static JumpFloorPath InspectWalk(Vector3 from, Vector3 to)
    {
        var distance = Vector3.Distance(from, to);
        if (distance > 2f || MathF.Abs(from.Y - to.Y) > 0.3f)
        {
            return JumpFloorPath.Blocked;
        }

        if (distance >= 0.1f && BGCollisionModule.RaycastMaterialFilter(
            from + Vector3.UnitY * 0.5f, (to - from) / distance, out _, distance))
        {
            return JumpFloorPath.Blocked;
        }

        if (!HasFloor(from))
        {
            return JumpFloorPath.Unavailable;
        }

        var samples = Math.Max(1, (int)MathF.Ceiling(distance / 0.1f));
        for (var index = 0; index <= samples; index++)
        {
            var point = Vector3.Lerp(from, to, index / (float)samples);
            if (!HasFloor(point))
            {
                return JumpFloorPath.Blocked;
            }
        }

        return JumpFloorPath.Supported;
    }

    private static bool HasFloor(Vector3 point)
    {
        if (!BGCollisionModule.RaycastMaterialFilter(point + Vector3.UnitY * 0.3f, -Vector3.UnitY, out var hit, 0.6f))
        {
            return false;
        }

        return JumpSurface.IsWalkable(hit.V1, hit.V2, hit.V3, hit.Normal);
    }
}
