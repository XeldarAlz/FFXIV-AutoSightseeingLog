using AutoSightseeingLog.Core.Ipc;
using AutoSightseeingLog.Core.Vistas;
using Dalamud.Game.ClientState.Conditions;
using ECommons.DalamudServices;
using System.Globalization;
using System.Threading.Tasks;

namespace AutoSightseeingLog.Core.Tasks;

internal abstract partial class AutoCommon
{
    // A teleport to Gridania, two short talks and a cutscene; the cap only catches a questing plugin that never gets there.
    private const int UnlockQuestTimeoutMs = 20 * TimeUnits.MillisecondsPerMinute;
    private const int UnlockQuestPollMs = 1_000;
    // The questing plugin reports itself idle for a moment after a start, before its first step is queued.
    private const int UnlockQuestStartGraceMs = 15_000;
    // After the journal flips, the questing plugin still clicks through the reward dialog and the cutscene.
    private const int UnlockWindDownMs = 60_000;
    private const int UnlockSettleMs = 30_000;
    private const string UnlockScope = "unlock-log";
    private const string UnlockStopReason = "Auto Sightseeing Log";

    // The Sightseeing Log records nothing until its quest is done, so a run on a fresh character starts by handing that
    // quest to the questing plugin and watching the journal. True once the log is unlocked.
    protected async Task<bool> UnlockSightseeingLog()
    {
        var questId = VistaLog.QuestJournalId(VistaData.UnlockQuestId).ToString(CultureInfo.InvariantCulture);
        var questName = GameNames.Quest(VistaData.UnlockQuestId);
        var questionable = QuestionableIPC.Instance;
        if (questionable.IsRunning())
        {
            Warn($"{UnlockScope}: Questionable is busy; it has to be idle before it can take '{questName}'");
            Svc.Chat.PrintError($"{AslConstants.LogPrefix} Your Sightseeing Log is locked, and Questionable is busy with something else. Stop it, then start again.");
            return false;
        }

        if (questionable.IsQuestLocked(questId))
        {
            var prerequisite = GameNames.Quest(VistaData.UnlockQuestPrerequisiteId);
            Warn($"{UnlockScope}: Questionable reports '{questName}' as locked; the main scenario has not reached it yet");
            Svc.Chat.PrintError($"{AslConstants.LogPrefix} Your Sightseeing Log is locked, and “{questName}” cannot be taken yet. Play the main scenario through “{prerequisite}”, then start again.");
            return false;
        }

        Diag($"{UnlockScope}: the Sightseeing Log is locked; asking Questionable to complete quest {questId} '{questName}'");
        Svc.Chat.Print($"{AslConstants.LogPrefix} Your Sightseeing Log is locked. Questionable is completing “{questName}” first.");
        var start = questionable.Start(questId);
        if (start == QuestStart.Refused)
        {
            Warn($"{UnlockScope}: Questionable did not accept quest {questId}");
            Svc.Chat.PrintError($"{AslConstants.LogPrefix} Questionable did not accept “{questName}”. Complete it yourself, then start again.");
            return false;
        }

        Diag($"{UnlockScope}: Questionable accepted quest {questId} ({start})");
        Status = $"Questionable is completing {questName}";
        var completed = false;
        try
        {
            completed = await WatchUnlockQuest(questionable, questId, questName);
            if (completed)
            {
                await WaitUntilTimed(() => !questionable.IsRunning() || questionable.CurrentQuestId() != questId, UnlockWindDownMs, "unlock-wind-down");
            }
        }
        finally
        {
            // Single-quest mode ends by itself; a build that keeps questing, a cancelled run and a timeout all leave it going.
            if (questionable.IsRunning())
            {
                Diag($"{UnlockScope}: stopping Questionable");
                questionable.Stop(UnlockStopReason);
            }
        }

        if (!completed)
        {
            if (!CancelToken.IsCancellationRequested && Svc.ClientState.IsLoggedIn)
            {
                Svc.Chat.PrintError($"{AslConstants.LogPrefix} Questionable did not finish “{questName}”. Complete it yourself, then start again.");
            }

            return false;
        }

        await WaitUntilTimed(IsFreeAfterQuest, UnlockSettleMs, "unlock-settle");
        VistaLog.Refresh(force: true);
        Diag($"{UnlockScope}: '{questName}' is complete; the Sightseeing Log is unlocked");
        Svc.Chat.Print($"{AslConstants.LogPrefix} “{questName}” is complete and your Sightseeing Log is unlocked. On with the tour.");
        return true;
    }

    private async Task<bool> WatchUnlockQuest(QuestionableIPC questionable, string questId, string questName)
    {
        var startedAt = Environment.TickCount64;
        var deadline = startedAt + UnlockQuestTimeoutMs;
        while (!CancelToken.IsCancellationRequested && Environment.TickCount64 < deadline)
        {
            if (VistaLog.QuestCompleted(VistaData.UnlockQuestId))
            {
                return true;
            }

            if (!Svc.ClientState.IsLoggedIn)
            {
                Warn($"{UnlockScope}: the character logged out while Questionable was on '{questName}'");
                return false;
            }

            if (questionable.CurrentQuestId() is { } current && current != questId)
            {
                Warn($"{UnlockScope}: Questionable moved on to quest {current} before '{questName}' was complete");
                return false;
            }

            if (Environment.TickCount64 - startedAt > UnlockQuestStartGraceMs && !questionable.IsRunning())
            {
                Warn($"{UnlockScope}: Questionable stopped before '{questName}' was complete");
                return false;
            }

            await DelayMs(UnlockQuestPollMs);
        }

        if (!CancelToken.IsCancellationRequested)
        {
            Warn($"{UnlockScope}: '{questName}' was not complete within {UnlockQuestTimeoutMs / TimeUnits.MillisecondsPerMinute} minutes");
        }

        return false;
    }

    private static bool IsFreeAfterQuest()
        => !Svc.Condition[ConditionFlag.OccupiedInEvent]
        && !Svc.Condition[ConditionFlag.OccupiedInQuestEvent]
        && !Svc.Condition[ConditionFlag.OccupiedInCutSceneEvent]
        && !Svc.Condition[ConditionFlag.WatchingCutscene]
        && !Svc.Condition[ConditionFlag.BetweenAreas];
}
