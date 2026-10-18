using System.Numerics;

namespace AutoSightseeingLog.Core.Vistas;

internal enum JumpRecoveryFailure { None, TooFar, FloorUnavailable, GapOrSlope, Obstacle, WrongLevel }

// Recovery follows the collision surface, not a straight line interpolated between endpoint heights.
// Keeping projection separate from game calls lets us check roofs, gaps and stacked floors offline.
internal sealed class JumpRecoveryPath(List<Vector3> points)
{
    private const float SampleSpacing = 0.2f;
    public const float MaxDistance = 18f;
    public IReadOnlyList<Vector3> Points => points;

    public static IEnumerable<int> Checkpoints(JumpRoute route, int lastReachedLeg)
    {
        for (var leg = Math.Min(lastReachedLeg, route.Length - 2); leg > 0; leg--)
        {
            if (!route[leg].Jumps && !route[leg].Drops && route[leg + 1].Jumps)
            {
                yield return leg;
            }
        }
    }

    public static bool TryCreate(Vector3 from, Vector3 to, Func<Vector3, Vector3?> floorAt,
        Func<Vector3, Vector3, bool> clear, out JumpRecoveryPath? path, out JumpRecoveryFailure failure)
    {
        path = null;
        failure = JumpRecoveryFailure.TooFar;
        var line = new Vector2(to.X - from.X, to.Z - from.Z);
        var distance = line.Length();
        if (distance > MaxDistance || MathF.Abs(to.Y - from.Y) > 8f)
        {
            return false;
        }

        failure = JumpRecoveryFailure.FloorUnavailable;
        if (floorAt(from) is not { } start || MathF.Abs(start.Y - from.Y) > 0.2f)
        {
            return false;
        }

        var points = new List<Vector3> { start };
        var count = Math.Max(1, (int)MathF.Ceiling(distance / SampleSpacing));
        var side = distance > 0.001f ? new Vector3(-line.Y, 0f, line.X) / distance * 0.1f : Vector3.UnitX * 0.1f;
        for (var index = 1; index <= count; index++)
        {
            var expected = Vector3.Lerp(from, to, index / (float)count);
            expected.Y = points[^1].Y;
            failure = JumpRecoveryFailure.GapOrSlope;
            if (floorAt(expected) is not { } next
                || MathF.Abs(next.Y - expected.Y) > distance / count * 1.4f + 0.03f
                || floorAt(next + side) is not { } left || MathF.Abs(left.Y - next.Y) > 0.2f
                || floorAt(next - side) is not { } right || MathF.Abs(right.Y - next.Y) > 0.2f)
            {
                return false;
            }

            failure = JumpRecoveryFailure.Obstacle;
            if (!clear(points[^1], next) || !clear(points[^1] + side, next + side)
                || !clear(points[^1] - side, next - side))
            {
                return false;
            }

            points.Add(next);
        }

        failure = JumpRecoveryFailure.WrongLevel;
        if (MathF.Abs(points[^1].Y - to.Y) > 0.2f)
        {
            return false;
        }

        failure = JumpRecoveryFailure.None;
        path = new(points);
        return true;
    }

    public bool Contains(Vector3 position)
    {
        var horizontal = new Vector2(position.X, position.Z);
        for (var index = 1; index < points.Count; index++)
        {
            var from = new Vector2(points[index - 1].X, points[index - 1].Z);
            var to = new Vector2(points[index].X, points[index].Z);
            var delta = to - from;
            var along = delta.LengthSquared() > 0f
                ? Math.Clamp(Vector2.Dot(horizontal - from, delta) / delta.LengthSquared(), 0f, 1f) : 0f;
            if (Vector2.Distance(horizontal, Vector2.Lerp(from, to, along)) > 0.25f)
            {
                continue;
            }

            var height = float.Lerp(points[index - 1].Y, points[index].Y, along);
            if (position.Y >= height - 0.2f && position.Y <= height + 0.3f)
            {
                return true;
            }
        }

        return false;
    }
}
