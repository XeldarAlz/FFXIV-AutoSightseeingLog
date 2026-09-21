using ECommons.DalamudServices;
using Lumina.Data.Files;
using Lumina.Data.Parsing.Layer;
using Lumina.Excel.Sheets;
using System.Numerics;

namespace AutoSightseeingLog.Core.Vistas;

internal static class VistaVolumes
{
    private const string LayoutFileName = "planlive.lgb";
    private const float FallbackRadiusMeters = 0.5f;
    private const float FallbackHalfHeightMeters = 1f;

    private static readonly Dictionary<uint, Dictionary<uint, VistaVolume>> byTerritory = [];

    public static VistaVolume Of(in Vista vista)
    {
        if (!byTerritory.TryGetValue(vista.TerritoryId, out var volumes))
        {
            volumes = Load(vista.TerritoryId);
            byTerritory[vista.TerritoryId] = volumes;
        }

        return volumes.TryGetValue(vista.LevelId, out var volume) ? volume : FromLevelRow(vista);
    }

    private static Dictionary<uint, VistaVolume> Load(uint territoryId)
    {
        var volumes = new Dictionary<uint, VistaVolume>();
        if (Svc.Data.GetExcelSheet<TerritoryType>().GetRowOrDefault(territoryId) is not { } territory)
        {
            return volumes;
        }

        var background = territory.Bg.ToString();
        var folderEnd = background.LastIndexOf('/');
        if (folderEnd <= 0)
        {
            return volumes;
        }

        var path = $"bg/{background[..folderEnd]}/{LayoutFileName}";
        try
        {
            if (Svc.Data.GetFile<LgbFile>(path) is { } layout)
            {
                Collect(layout, WantedLevelIds(territoryId), volumes);
            }
        }
        catch (Exception exception)
        {
            Svc.Log.Warning($"{AslConstants.LogPrefix} Could not read the vista volumes from {path}: {exception.Message}");
        }

        return volumes;
    }

    private static HashSet<uint> WantedLevelIds(uint territoryId)
    {
        var wanted = new HashSet<uint>();
        var vistas = VistaRegistry.All;
        for (var index = 0; index < vistas.Length; index++)
        {
            if (vistas[index].TerritoryId == territoryId)
            {
                wanted.Add(vistas[index].LevelId);
            }
        }

        return wanted;
    }

    private static void Collect(LgbFile layout, HashSet<uint> wanted, Dictionary<uint, VistaVolume> volumes)
    {
        var layers = layout.Layers;
        for (var layerIndex = 0; layerIndex < layers.Length; layerIndex++)
        {
            var objects = layers[layerIndex].InstanceObjects;
            for (var objectIndex = 0; objectIndex < objects.Length; objectIndex++)
            {
                var instance = objects[objectIndex];
                if (!wanted.Contains(instance.InstanceId) || instance.Object is not LayerCommon.EventRangeInstanceObject range)
                {
                    continue;
                }

                var transform = instance.Transform;
                volumes[instance.InstanceId] = VistaVolume.Create(
                    new Vector3(transform.Translation.X, transform.Translation.Y, transform.Translation.Z),
                    new Vector3(transform.Scale.X, transform.Scale.Y, transform.Scale.Z),
                    transform.Rotation.Y,
                    range.ParentData.TriggerBoxShape == TriggerBoxShape.TriggerBoxShapeCylinder);
            }
        }
    }

    private static VistaVolume FromLevelRow(in Vista vista)
    {
        var level = Svc.Data.GetExcelSheet<Level>().GetRowOrDefault(vista.LevelId);
        var radius = level is { Radius: > 0f } row ? row.Radius : FallbackRadiusMeters;
        return VistaVolume.Create(vista.Position, new Vector3(radius, FallbackHalfHeightMeters, radius), level?.Yaw ?? 0f, isCylinder: true);
    }
}
