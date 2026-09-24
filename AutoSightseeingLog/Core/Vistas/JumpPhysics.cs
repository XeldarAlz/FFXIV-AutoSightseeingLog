using Dalamud.Game.ClientState.Conditions;
using ECommons.DalamudServices;

namespace AutoSightseeingLog.Core.Vistas;

internal static class JumpPhysics
{
    private const float RunUpStartLatencySeconds = 0.05f;
    private const float RunUpMetersPerSecond = 3.7f;

    public static float RunUpMeters(float runUpSeconds)
        => MathF.Max(0f, (runUpSeconds - RunUpStartLatencySeconds) * RunUpMetersPerSecond);

    public static float RunUpSeconds(float runUpMeters)
        => (runUpMeters / RunUpMetersPerSecond) + RunUpStartLatencySeconds;

    public static bool IsAirborne()
        => Svc.Condition[ConditionFlag.Jumping] || Svc.Condition[ConditionFlag.Jumping61];
}
