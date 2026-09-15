using ECommons.DalamudServices;
using Lumina.Excel.Sheets;
using System.Numerics;

namespace AutoSightseeingLog.Core.Vistas;

internal readonly record struct VistaZone(uint TerritoryId, int Start, int Count);

// The Adventure sheet is the Sightseeing Log in order: row index plus one is the number the log shows.
internal static class VistaRegistry
{
    private const int ExpansionCount = (int)ExpansionKind.Dawntrail + 1;
    private const int HoursPerTimeValue = 100;

    private static Catalog? catalog;

    private sealed record Catalog(Vista[] Vistas, string[] Names, Vista[][] ByZone, VistaZone[][] Zones, uint[] GateQuests);

    private readonly record struct PhaseRange(uint FirstAdventureId, uint LastAdventureId, uint QuestId);

    private static Catalog Data => catalog ??= Build();

    public static ReadOnlySpan<Vista> All => Data.Vistas;

    public static int Count => Data.Vistas.Length;

    public static ReadOnlySpan<uint> GateQuests => Data.GateQuests;

    public static ReadOnlySpan<Vista> ByZone(ExpansionKind expansion) => Data.ByZone[(int)expansion];

    public static ReadOnlySpan<VistaZone> Zones(ExpansionKind expansion) => Data.Zones[(int)expansion];

    public static bool TryGet(ushort number, out Vista vista)
    {
        var vistas = Data.Vistas;
        if (number == 0 || number > vistas.Length)
        {
            vista = default;
            return false;
        }

        vista = vistas[number - 1];
        return true;
    }

    public static string Name(ushort number)
    {
        var names = Data.Names;
        return number >= 1 && number <= names.Length ? names[number - 1] : string.Empty;
    }

    private static Catalog Build()
    {
        var adventures = Svc.Data.GetExcelSheet<Adventure>();
        var territories = Svc.Data.GetExcelSheet<TerritoryType>();
        var phases = LoadPhases();
        var vistas = new Vista[adventures.Count];
        var names = new string[adventures.Count];
        var gateQuests = new List<uint>();
        for (var rowIndex = 0; rowIndex < adventures.Count; rowIndex++)
        {
            var adventure = adventures.GetRowAt(rowIndex);
            var number = (ushort)(rowIndex + 1);
            var level = adventure.Level.ValueNullable;
            var territoryId = level?.Territory.RowId ?? 0;
            var position = level is { } levelRow ? new Vector3(levelRow.X, levelRow.Y, levelRow.Z) : Vector3.Zero;
            var expansion = ExpansionOf(territories.GetRowOrDefault(territoryId));
            var (gate, questId) = GateOf(number, adventure.RowId, expansion, phases);
            if (questId != 0 && !gateQuests.Contains(questId))
            {
                gateQuests.Add(questId);
            }

            var approachPoint = VistaData.TryGetApproachPoint(number, out var point) ? point : position;
            vistas[rowIndex] = new Vista(
                number,
                expansion,
                territoryId,
                position,
                approachPoint,
                (ushort)adventure.Emote.RowId,
                (byte)(adventure.MinTime / HoursPerTimeValue),
                (byte)(adventure.MaxTime / HoursPerTimeValue),
                adventure.MinTime != 0 || adventure.MaxTime != 0,
                expansion == ExpansionKind.ARealmReborn ? VistaData.WeatherMask(number) : 0u,
                gate,
                questId,
                VistaData.Approach(number));
            names[rowIndex] = GameText.Plain(adventure.Name);
        }

        var (byZone, zones) = GroupByZone(vistas);
        return new Catalog(vistas, names, byZone, zones, [.. gateQuests]);
    }

    private static ExpansionKind ExpansionOf(TerritoryType? territory)
    {
        if (territory is not { } row)
        {
            return ExpansionKind.ARealmReborn;
        }

        return (ExpansionKind)Math.Min(row.ExVersion.RowId, (uint)ExpansionKind.Dawntrail);
    }

    private static (VistaGate Gate, uint QuestId) GateOf(ushort number, uint adventureId, ExpansionKind expansion, PhaseRange[] phases)
    {
        if (expansion == ExpansionKind.ARealmReborn)
        {
            return number <= VistaData.FirstLogCount ? (VistaGate.None, 0u) : (VistaGate.FirstLogRecorded, 0u);
        }

        if (expansion == ExpansionKind.Heavensward)
        {
            return (VistaGate.Quest, VistaData.HeavenswardQuestId);
        }

        for (var index = 0; index < phases.Length; index++)
        {
            var phase = phases[index];
            if (adventureId >= phase.FirstAdventureId && adventureId <= phase.LastAdventureId)
            {
                return (VistaGate.Quest, phase.QuestId);
            }
        }

        return (VistaGate.None, 0u);
    }

    private static PhaseRange[] LoadPhases()
    {
        var sheet = Svc.Data.GetExcelSheet<AdventureExPhase>();
        var ranges = new PhaseRange[sheet.Count];
        for (var index = 0; index < sheet.Count; index++)
        {
            var phase = sheet.GetRowAt(index);
            ranges[index] = new PhaseRange(phase.AdventureBegin.RowId, phase.AdventureEnd.RowId, phase.Quest.RowId);
        }

        return ranges;
    }

    // The log lists a zone's vistas in more than one run (Heavensward revisits every zone after its first pass), so each
    // expansion is regrouped by zone, in the order the zones first appear.
    private static (Vista[][] ByZone, VistaZone[][] Zones) GroupByZone(Vista[] vistas)
    {
        var byZone = new Vista[ExpansionCount][];
        var zones = new VistaZone[ExpansionCount][];
        var zoneOrder = new List<uint>();
        var grouped = new List<Vista>();
        var zoneList = new List<VistaZone>();
        for (var expansionIndex = 0; expansionIndex < ExpansionCount; expansionIndex++)
        {
            var expansion = (ExpansionKind)expansionIndex;
            zoneOrder.Clear();
            grouped.Clear();
            zoneList.Clear();
            for (var index = 0; index < vistas.Length; index++)
            {
                if (vistas[index].Expansion == expansion && !zoneOrder.Contains(vistas[index].TerritoryId))
                {
                    zoneOrder.Add(vistas[index].TerritoryId);
                }
            }

            for (var zoneIndex = 0; zoneIndex < zoneOrder.Count; zoneIndex++)
            {
                var start = grouped.Count;
                for (var index = 0; index < vistas.Length; index++)
                {
                    if (vistas[index].Expansion == expansion && vistas[index].TerritoryId == zoneOrder[zoneIndex])
                    {
                        grouped.Add(vistas[index]);
                    }
                }

                zoneList.Add(new VistaZone(zoneOrder[zoneIndex], start, grouped.Count - start));
            }

            byZone[expansionIndex] = [.. grouped];
            zones[expansionIndex] = [.. zoneList];
        }

        return (byZone, zones);
    }
}
