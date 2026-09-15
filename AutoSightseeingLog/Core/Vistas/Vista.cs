using System.Numerics;

namespace AutoSightseeingLog.Core.Vistas;

internal readonly record struct Vista(
    ushort Number,
    ExpansionKind Expansion,
    uint TerritoryId,
    Vector3 Position,
    Vector3 ApproachPoint,
    ushort EmoteId,
    byte FirstBell,
    byte LastBell,
    bool HasTimeWindow,
    uint WeatherMask,
    VistaGate Gate,
    uint GateQuestId,
    VistaApproach Approach)
{
    public bool HasConditions => HasTimeWindow || WeatherMask != 0;
}
