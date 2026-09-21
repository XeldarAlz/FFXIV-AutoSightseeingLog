using FFXIVClientStructs.FFXIV.Client.Game.Control;

namespace AutoSightseeingLog.Core.Game.Ops;

internal static unsafe class WalkModeOps
{
    public static bool IsWalking()
    {
        var control = Control.Instance();
        return control != null && control->IsWalking;
    }

    public static void SetWalking(bool walking)
    {
        var control = Control.Instance();
        if (control == null)
        {
            return;
        }

        control->IsWalking = walking;
    }
}
