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
// WigglyQuest is a Questionable fork that answers the same calls under its own prefix and has no Stop call; it is the
// one asked when both are loaded.
internal sealed class QuestionableIPC
{
    private const string IsRunningFailed = "Questionable IsRunning failed";
    private const string CurrentQuestFailed = "Questionable GetCurrentQuestId failed";
    private const string IsQuestLockedFailed = "Questionable IsQuestLocked failed";
    private const string StartSingleQuestFailed = "Questionable StartSingleQuest failed";
    private const string StartQuestFailed = "Questionable StartQuest failed";
    private const string StopFailed = "Questionable Stop failed";

    private static QuestionableIPC? instance;

    private readonly QuestingPlugin wigglyQuest = new("WigglyQuest", "/wqst");
    private readonly QuestingPlugin questionable = new("Questionable", "/qst");

    private QuestionableIPC()
    {
    }

    public static QuestionableIPC Instance => instance ??= new QuestionableIPC();

    private QuestingPlugin Active => wigglyQuest.IsRunning.HasFunction ? wigglyQuest : questionable;

    public bool IsRunning()
    {
        var active = Active;
        return IpcGate.Invoke(active.IsRunning.HasFunction, active.IsRunningCall, false, IsRunningFailed);
    }

    public string? CurrentQuestId()
    {
        var active = Active;
        return IpcGate.Invoke(active.CurrentQuestId.HasFunction, active.CurrentQuestIdCall, null, CurrentQuestFailed);
    }

    // The far side answers true for a quest whose prerequisites are not met and for one it has no route for. A build
    // without the gate cannot say, so the quest is treated as open and the start itself is what fails.
    public bool IsQuestLocked(string questId)
    {
        var active = Active;
        return IpcGate.Invoke(active.IsQuestLocked.HasFunction, () => active.IsQuestLocked.InvokeFunc(questId), false, IsQuestLockedFailed);
    }

    public QuestStart Start(string questId)
    {
        var active = Active;
        if (active.StartSingleQuest.HasFunction)
        {
            return IpcGate.Invoke(true, () => active.StartSingleQuest.InvokeFunc(questId), false, StartSingleQuestFailed) ? QuestStart.Single : QuestStart.Refused;
        }

        if (active.StartQuest.HasFunction)
        {
            return IpcGate.Invoke(true, () => active.StartQuest.InvokeFunc(questId), false, StartQuestFailed) ? QuestStart.Continuous : QuestStart.Refused;
        }

        Chat.ExecuteCommand(string.Concat(active.NextQuestCommand, questId));
        Chat.ExecuteCommand(active.StartCommand);
        return QuestStart.Continuous;
    }

    public void Stop(string reason)
    {
        var active = Active;
        if (active.Stop.HasFunction)
        {
            IpcGate.Run(true, () => active.Stop.InvokeFunc(reason), StopFailed);
            return;
        }

        Chat.ExecuteCommand(active.StopCommand);
    }

    private sealed class QuestingPlugin
    {
        public QuestingPlugin(string ipcPrefix, string command)
        {
            var pluginInterface = Svc.PluginInterface;
            IsRunning = pluginInterface.GetIpcSubscriber<bool>(ipcPrefix + ".IsRunning");
            CurrentQuestId = pluginInterface.GetIpcSubscriber<string?>(ipcPrefix + ".GetCurrentQuestId");
            IsQuestLocked = pluginInterface.GetIpcSubscriber<string, bool>(ipcPrefix + ".IsQuestLocked");
            StartSingleQuest = pluginInterface.GetIpcSubscriber<string, bool>(ipcPrefix + ".StartSingleQuest");
            StartQuest = pluginInterface.GetIpcSubscriber<string, bool>(ipcPrefix + ".StartQuest");
            Stop = pluginInterface.GetIpcSubscriber<string, bool>(ipcPrefix + ".Stop");

            IsRunningCall = IsRunning.InvokeFunc;
            CurrentQuestIdCall = CurrentQuestId.InvokeFunc;

            NextQuestCommand = command + " next ";
            StartCommand = command + " start";
            StopCommand = command + " stop";
        }

        public ICallGateSubscriber<bool> IsRunning { get; }
        public ICallGateSubscriber<string?> CurrentQuestId { get; }
        public ICallGateSubscriber<string, bool> IsQuestLocked { get; }
        public ICallGateSubscriber<string, bool> StartSingleQuest { get; }
        public ICallGateSubscriber<string, bool> StartQuest { get; }
        public ICallGateSubscriber<string, bool> Stop { get; }

        // Cached once so the per-second watch does not allocate a delegate on every call.
        public Func<bool> IsRunningCall { get; }
        public Func<string?> CurrentQuestIdCall { get; }

        public string NextQuestCommand { get; }
        public string StartCommand { get; }
        public string StopCommand { get; }
    }
}
