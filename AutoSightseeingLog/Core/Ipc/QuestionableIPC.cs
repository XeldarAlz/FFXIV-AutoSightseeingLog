using Dalamud.Plugin.Ipc;
using ECommons.Automation;
using ECommons.DalamudServices;

namespace AutoSightseeingLog.Core.Ipc;

internal enum QuestStart : byte
{
    Refused,
    // The questing plugin stops on its own once the quest is done.
    Single,
    // The questing plugin carries on to the next quest it knows, so the caller stops it.
    Continuous,
}

// The questing plugin drives the character through the same pathfinder this plugin uses, so it is only asked to run
// while this plugin's own movement is idle. A build without a gate falls back to the plugin's chat commands.
internal sealed class QuestionableIPC
{
    private const string IsRunningFailed = AslConstants.LogPrefix + " Questionable IsRunning failed";
    private const string CurrentQuestFailed = AslConstants.LogPrefix + " Questionable GetCurrentQuestId failed";
    private const string IsQuestLockedFailed = AslConstants.LogPrefix + " Questionable IsQuestLocked failed";
    private const string StartSingleQuestFailed = AslConstants.LogPrefix + " Questionable StartSingleQuest failed";
    private const string StartQuestFailed = AslConstants.LogPrefix + " Questionable StartQuest failed";
    private const string StopFailed = AslConstants.LogPrefix + " Questionable Stop failed";
    private const string NextQuestCommand = "/qst next ";
    private const string StartCommand = "/qst start";
    private const string StopCommand = "/qst stop";

    private static QuestionableIPC? instance;

    private readonly ICallGateSubscriber<bool> isRunning;
    private readonly ICallGateSubscriber<string?> currentQuestId;
    private readonly ICallGateSubscriber<string, bool> isQuestLocked;
    private readonly ICallGateSubscriber<string, bool> startSingleQuest;
    private readonly ICallGateSubscriber<string, bool> startQuest;
    private readonly ICallGateSubscriber<string, bool> stop;

    // Cached once so the per-second watch does not allocate a delegate on every call.
    private readonly Func<bool> isRunningCall;
    private readonly Func<string?> currentQuestIdCall;

    private QuestionableIPC()
    {
        var pluginInterface = Svc.PluginInterface;
        isRunning = pluginInterface.GetIpcSubscriber<bool>("Questionable.IsRunning");
        currentQuestId = pluginInterface.GetIpcSubscriber<string?>("Questionable.GetCurrentQuestId");
        isQuestLocked = pluginInterface.GetIpcSubscriber<string, bool>("Questionable.IsQuestLocked");
        startSingleQuest = pluginInterface.GetIpcSubscriber<string, bool>("Questionable.StartSingleQuest");
        startQuest = pluginInterface.GetIpcSubscriber<string, bool>("Questionable.StartQuest");
        stop = pluginInterface.GetIpcSubscriber<string, bool>("Questionable.Stop");

        isRunningCall = isRunning.InvokeFunc;
        currentQuestIdCall = currentQuestId.InvokeFunc;
    }

    public static QuestionableIPC Instance => instance ??= new QuestionableIPC();

    public bool IsRunning()
        => IpcGate.Invoke(isRunning.HasFunction, isRunningCall, false, IsRunningFailed);

    public string? CurrentQuestId()
        => IpcGate.Invoke(currentQuestId.HasFunction, currentQuestIdCall, null, CurrentQuestFailed);

    // The far side answers true for a quest whose prerequisites are not met and for one it has no route for. A build
    // without the gate cannot say, so the quest is treated as open and the start itself is what fails.
    public bool IsQuestLocked(string questId)
        => IpcGate.Invoke(isQuestLocked.HasFunction, () => isQuestLocked.InvokeFunc(questId), false, IsQuestLockedFailed);

    public QuestStart Start(string questId)
    {
        if (startSingleQuest.HasFunction)
        {
            return IpcGate.Invoke(true, () => startSingleQuest.InvokeFunc(questId), false, StartSingleQuestFailed) ? QuestStart.Single : QuestStart.Refused;
        }

        if (startQuest.HasFunction)
        {
            return IpcGate.Invoke(true, () => startQuest.InvokeFunc(questId), false, StartQuestFailed) ? QuestStart.Continuous : QuestStart.Refused;
        }

        Chat.ExecuteCommand(string.Concat(NextQuestCommand, questId));
        Chat.ExecuteCommand(StartCommand);
        return QuestStart.Continuous;
    }

    public void Stop(string reason)
    {
        if (stop.HasFunction)
        {
            IpcGate.Run(true, () => stop.InvokeFunc(reason), StopFailed);
            return;
        }

        Chat.ExecuteCommand(StopCommand);
    }
}
