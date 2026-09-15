using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace AutoSightseeingLog.Core.Game.Ops;

// By emote id rather than by typed command, so it works the same in every client language.
internal static unsafe class EmoteOps
{
    public static bool CanUse(ushort emoteId)
    {
        var agent = AgentEmote.Instance();
        return agent != null && agent->CanUseEmote(emoteId);
    }

    // Kept out of the player's recent emote history, which the emote window shows.
    public static bool Execute(ushort emoteId)
    {
        var agent = AgentEmote.Instance();
        if (agent == null)
        {
            return false;
        }

        agent->ExecuteEmote(emoteId, null, false, false);
        return true;
    }
}
