using AutoSightseeingLog.Core.Vistas;
using System.Numerics;

namespace AutoSightseeingLog.Core.Tasks;

internal static class JumpLanding
{
    // Called only after observed touchdown and stable grounding at the expected height. The
    // recorded point is the known landing/takeoff position; an unavailable ray is not a veto.
    // This exception never authorizes arbitrary recovery walks between different ledges.
    public static bool CanCorrect(Vector3 from, Vector3 target, bool recorded, JumpFloorPath floor)
        => floor != JumpFloorPath.Blocked && JumpPosition.OnPoint(from, target, 1f,
            floor == JumpFloorPath.Supported ? 0.3f : 0.1f)
        && (recorded || floor == JumpFloorPath.Supported);

    public static bool RetainedFoothold(JumpRoute route, Vector3 position, int missedLeg)
    {
        // A launch rejected on its starting ledge and a blocked landing correction are not falls.
        // Do not turn either into a walk back down the tower. Other-level falls still use recovery.
        for (var leg = Math.Max(1, missedLeg - 1); leg <= Math.Min(missedLeg, route.Length - 1); leg++)
        {
            if (JumpPosition.OnPoint(position, route[leg].Point, 1f, 0.1f))
            {
                return true;
            }
        }

        return false;
    }

    // Ground navigation can snap an off-mesh balcony onto the street beneath it. Returning to the
    // entrance is only a ground-level fallback; elevated platforms need an actual route connection.
    public static bool IsAtRouteGroundLevel(Vector3 position, Vector3 routeStart)
        => MathF.Abs(position.Y - routeStart.Y) <= 2f;

    // The aetheryte can be on a different street level from the puzzle entrance. Verify the
    // teleport destination itself, not the entrance height, before handing over to ground travel.
    // A city aetheryte's height is not in the game data, so only its ground distance can be checked.
    public static bool IsAtTeleportDestination(Vector3 position, Vector3 destination, bool knownHeight)
        => JumpPosition.OnPoint(position, destination, 20f, knownHeight ? 5f : float.PositiveInfinity);
}

internal sealed class JumpCorrectionGuard(Vector3 from, Vector3 target)
{
    public bool LostFooting(JumpSample sample)
        => sample.Airborne || sample.Position.Y < MathF.Min(from.Y, target.Y) - 0.08f
        || sample.Position.Y > MathF.Max(from.Y, target.Y) + 0.15f;
}
