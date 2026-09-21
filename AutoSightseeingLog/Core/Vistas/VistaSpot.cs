using System.Numerics;

namespace AutoSightseeingLog.Core.Vistas;

internal static class VistaSpot
{
    public static bool Active { get; private set; }

    public static bool HasFooting { get; private set; }

    public static bool AwaitingPlayer { get; private set; }

    public static uint TerritoryId { get; private set; }

    public static VistaVolume Volume { get; private set; }

    public static Vector3 Footing { get; private set; }

    public static void Show(in VistaVolume volume, uint territoryId)
    {
        Volume = volume;
        TerritoryId = territoryId;
        Active = true;
        HasFooting = false;
        AwaitingPlayer = false;
    }

    public static void ShowFooting(Vector3 footing)
    {
        Footing = footing;
        HasFooting = true;
    }

    public static void SetAwaitingPlayer(bool awaiting) => AwaitingPlayer = awaiting;

    public static void Clear()
    {
        Active = false;
        HasFooting = false;
        AwaitingPlayer = false;
    }
}
