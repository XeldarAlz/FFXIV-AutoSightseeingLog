using Dalamud.Game.ClientState.Conditions;
using ECommons.DalamudServices;
using ClientCharacter = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;

namespace AutoSightseeingLog.Core.Vistas;

internal static partial class JumpPhysics
{
    public static unsafe bool IsAirborne()
        => Svc.Condition[ConditionFlag.Jumping] || Svc.Condition[ConditionFlag.Jumping61]
        || (Svc.Objects.LocalPlayer is { } player && ((ClientCharacter*)player.Address)->IsJumping());
}
