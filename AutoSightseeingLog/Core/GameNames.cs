using ECommons.DalamudServices;
using Lumina.Excel.Sheets;

namespace AutoSightseeingLog.Core;

// Read from the game client, so every name matches what the player sees in their own client.
internal static class GameNames
{
    private static readonly Dictionary<uint, string> emoteCommands = new();
    private static readonly Dictionary<uint, string> weathers = new();
    private static readonly Dictionary<uint, string> quests = new();
    private static readonly Dictionary<uint, string> npcs = new();

    public static string EmoteCommand(uint emoteId) => Cached(emoteCommands, emoteId, static id =>
        Svc.Data.GetExcelSheet<Emote>().GetRowOrDefault(id)?.TextCommand.ValueNullable is { } command ? GameText.Plain(command.Command) : string.Empty);

    public static string Weather(uint weatherId) => Cached(weathers, weatherId, static id =>
        Svc.Data.GetExcelSheet<Weather>().GetRowOrDefault(id) is { } weather ? GameText.Plain(weather.Name) : string.Empty);

    public static string Quest(uint questId) => Cached(quests, questId, static id =>
        Svc.Data.GetExcelSheet<Quest>().GetRowOrDefault(id) is { } quest ? GameText.Plain(quest.Name) : string.Empty);

    public static string Npc(uint npcId) => Cached(npcs, npcId, static id =>
        Svc.Data.GetExcelSheet<ENpcResident>().GetRowOrDefault(id) is { } npc ? GameText.Plain(npc.Singular) : string.Empty);

    private static string Cached(Dictionary<uint, string> cache, uint id, Func<uint, string> resolve)
    {
        if (cache.TryGetValue(id, out var cached))
        {
            return cached;
        }

        var resolved = resolve(id);
        cache[id] = resolved;
        return resolved;
    }
}
