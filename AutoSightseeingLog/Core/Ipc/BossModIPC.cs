using Dalamud.Plugin.Ipc;
using ECommons.DalamudServices;

namespace AutoSightseeingLog.Core.Ipc;

// This plugin never fights, but a combat plugin the player runs alongside it can still steer the character, so only
// its automatic movement is touched here. Both editions register the same call gates, and a missing one is a no-op.
internal sealed class BossModIPC
{
    private const string PauseMovementFailed = AslConstants.LogPrefix + " BossMod AI.PauseMovement failed";
    private const string IsNavigatingFailed = AslConstants.LogPrefix + " BossMod AI.IsNavigating failed";
    private const string IsMovingFailed = AslConstants.LogPrefix + " BossMod Movement.IsMoving failed";
    private const string ConfigurationFailed = AslConstants.LogPrefix + " BossMod Configuration failed";
    private const string MovementConfigType = "AIConfig";
    private const string MovementForbiddenField = "ForbidMovement";

    private static BossModIPC? instance;

    // The far side answers the pause with the value it stored, so the gate is a function, not an action.
    private readonly ICallGateSubscriber<bool, bool> pauseMovement;
    private readonly ICallGateSubscriber<bool> isNavigating;
    private readonly ICallGateSubscriber<bool> isMoving;
    private readonly ICallGateSubscriber<List<string>, bool, List<string>> configuration;
    private readonly List<string> movementForbiddenQuery = [MovementConfigType, MovementForbiddenField];

    // Cached once so per-tick checks do not allocate a delegate on every call.
    private readonly Func<bool> isNavigatingCall;
    private readonly Func<bool> isMovingCall;
    private readonly Action forbidMovementCall;
    private readonly Action allowMovementCall;

    private bool movementHeld;
    private bool movementForbiddenByUser;

    private BossModIPC()
    {
        var pluginInterface = Svc.PluginInterface;
        pauseMovement = pluginInterface.GetIpcSubscriber<bool, bool>("BossMod.AI.PauseMovement");
        isNavigating = pluginInterface.GetIpcSubscriber<bool>("BossMod.AI.IsNavigating");
        isMoving = pluginInterface.GetIpcSubscriber<bool>("BossMod.Movement.IsMoving");
        configuration = pluginInterface.GetIpcSubscriber<List<string>, bool, List<string>>("BossMod.Configuration");

        isNavigatingCall = isNavigating.InvokeFunc;
        isMovingCall = isMoving.InvokeFunc;
        forbidMovementCall = () => pauseMovement.InvokeFunc(true);
        allowMovementCall = () => pauseMovement.InvokeFunc(false);
    }

    public static BossModIPC Instance => instance ??= new BossModIPC();

    public bool MovementHeld => movementHeld;

    // The combat plugin's automatic movement keeps steering toward its last target whenever the pathfinder is idle, and
    // while the character flies it never recomputes, so a stale target survives take-off and drags the mount off every
    // path this plugin queues. Held while this plugin drives the character and put back to the user's own setting
    // afterwards. True when this call is the one that took the hold.
    public bool HoldMovement()
    {
        if (movementHeld || !pauseMovement.HasFunction)
        {
            return false;
        }

        movementForbiddenByUser = ReadMovementForbidden();
        IpcGate.Run(pauseMovement.HasFunction, forbidMovementCall, PauseMovementFailed);
        movementHeld = true;
        return true;
    }

    public bool ReleaseMovement()
    {
        if (!movementHeld)
        {
            return false;
        }

        movementHeld = false;
        IpcGate.Run(pauseMovement.HasFunction, movementForbiddenByUser ? forbidMovementCall : allowMovementCall, PauseMovementFailed);
        return true;
    }

    public bool IsNavigating()
        => IpcGate.Invoke(isNavigating.HasFunction, isNavigatingCall, false, IsNavigatingFailed);

    public bool IsForcingMovement()
        => IpcGate.Invoke(isMoving.HasFunction, isMovingCall, false, IsMovingFailed);

    // The console gate answers a field query with that field's current value as its only line.
    private bool ReadMovementForbidden()
    {
        var answer = IpcGate.Invoke<List<string>?>(configuration.HasFunction, () => configuration.InvokeFunc(movementForbiddenQuery, false), null, ConfigurationFailed);
        return answer is { Count: 1 } && bool.TryParse(answer[0], out var forbidden) && forbidden;
    }
}
