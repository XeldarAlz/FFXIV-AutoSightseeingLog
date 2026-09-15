using ECommons.DalamudServices;
using Lumina.Excel.Sheets;

namespace AutoSightseeingLog.Core.Travel;

internal static class TerritoryNames
{
    private static readonly Dictionary<uint, string> names = new();

    public static string Of(uint territoryId)
    {
        if (names.TryGetValue(territoryId, out var cached))
        {
            return cached;
        }

        var placeName = Svc.Data.GetExcelSheet<TerritoryType>().GetRowOrDefault(territoryId)?.PlaceName.ValueNullable;
        var name = placeName is { } row ? GameText.Plain(row.Name) : string.Empty;
        names[territoryId] = name;
        return name;
    }
}
