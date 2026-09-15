using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using ClientCharacter = FFXIVClientStructs.FFXIV.Client.Game.Character.Character;

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

    // Emotes with a mode, such as Sit on Ground, hold the character in their pose until it stands up.
    public static bool EntersPose(ushort emoteId)
        => Svc.Data.GetExcelSheet<Emote>().GetRowOrDefault(emoteId) is { } emote && emote.EmoteMode.RowId != 0;

    public static bool InPose()
    {
        var player = Svc.Objects.LocalPlayer;
        return player != null && ((ClientCharacter*)player.Address)->Mode == CharacterModes.InPositionLoop;
    }
}
