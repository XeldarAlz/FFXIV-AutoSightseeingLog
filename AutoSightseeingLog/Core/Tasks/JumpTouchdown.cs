using ECommons.DalamudServices;
using System.Numerics;

namespace AutoSightseeingLog.Core.Tasks;

internal sealed class JumpTouchdown
{
    private bool tookOff;

    public Vector3? Position { get; private set; }

    public bool Check(bool airborne)
    {
        if (Position.HasValue)
        {
            return true;
        }

        if (airborne)
        {
            tookOff = true;
            return false;
        }

        if (!tookOff || Svc.Objects.LocalPlayer is not { } player)
        {
            return false;
        }

        Position = player.Position;
        return true;
    }
}
