using AutoSightseeingLog.Core.Game.Ops;
using AutoSightseeingLog.Core.Time;
using AutoSightseeingLog.Core.Travel;
using AutoSightseeingLog.Core.Vistas;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using System.Numerics;

namespace AutoSightseeingLog.Core.Debug;

// /asl logdump: the log as the plugin reads it, the raw bitmasks behind it and the weather check, for comparing
// against what the game shows.
internal static unsafe class VistaDumper
{
    private static readonly ExpansionKind[] expansions =
    [
        ExpansionKind.ARealmReborn, ExpansionKind.Heavensward, ExpansionKind.Stormblood,
        ExpansionKind.Shadowbringers, ExpansionKind.Endwalker, ExpansionKind.Dawntrail,
    ];

    public static void Dump()
    {
        VistaLog.Refresh(force: true);
        var now = EorzeaTime.Now();
        Write($"loaded={VistaLog.Loaded} vistas={VistaRegistry.Count} eorzea={EorzeaTime.BellOf(now):00}:{EorzeaTime.MinuteOf(now):00} server={now}");

        var playerState = PlayerState.Instance();
        if (playerState != null)
        {
            Write($"completed={Convert.ToHexString(playerState->CompletedAdventures)}");
            Write($"unknownMask={Convert.ToHexString(playerState->UnkAdventureBitmask)}");
        }

        for (var index = 0; index < expansions.Length; index++)
        {
            var vistas = VistaRegistry.ByZone(expansions[index]);
            Write($"{expansions[index]}: {VistaLog.CountRecorded(vistas)}/{vistas.Length} recorded");
        }

        Write($"log unlocked={VistaLog.LogUnlocked}");
        Write($"first log recorded={VistaLog.FirstLogRecorded}");
        var gateQuests = VistaRegistry.GateQuests;
        var quests = Svc.Data.GetExcelSheet<Quest>();
        for (var index = 0; index < gateQuests.Length; index++)
        {
            var name = quests.GetRowOrDefault(gateQuests[index]) is { } quest ? GameText.Plain(quest.Name) : "?";
            Write($"gate quest {gateQuests[index]} '{name}' done={VistaLog.IsQuestDone(gateQuests[index])}");
        }

        DumpCurrentZone(now);
        Svc.Chat.Print($"{AslConstants.LogPrefix} Sightseeing Log state written to the plugin log.");
    }

    private static void DumpCurrentZone(long now)
    {
        uint territoryId = Svc.ClientState.TerritoryType;
        Write($"zone {territoryId} {TerritoryNames.Of(territoryId)}: forecast weather={ZoneWeather.WeatherAt(territoryId, now)} game weather={WeatherOps.Current()}");

        var player = Svc.Objects.LocalPlayer;
        var vistas = VistaRegistry.All;
        for (var index = 0; index < vistas.Length; index++)
        {
            ref readonly var vista = ref vistas[index];
            if (vista.TerritoryId != territoryId)
            {
                continue;
            }

            var distance = player is null ? float.NaN : Vector3.Distance(player.Position, vista.Position);
            var window = VistaLog.TryGetWindow(vista, now, out var found) ? $"{found.Start}..{found.End}" : "none";
            Write($"#{vista.Number:000} {VistaRegistry.Name(vista.Number)}: {VistaLog.Status(vista, now)} window={window} distance={distance:F1} approach={vista.Approach}");
        }
    }

    private static void Write(string line) => RunLog.Info(line);
}
