using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;

namespace AutoSightseeingLog.Core.Game.Ops;

internal static unsafe class FlightOps
{
    public static bool UnlockedIn(uint territoryId)
    {
        var playerState = PlayerState.Instance();
        if (playerState == null || Svc.Data.GetExcelSheet<TerritoryType>().GetRowOrDefault(territoryId) is not { } territory)
        {
            return false;
        }

        var completionSet = territory.AetherCurrentCompFlgSet.RowId;
        return completionSet != 0 && playerState->IsAetherCurrentZoneComplete(completionSet);
    }
}
