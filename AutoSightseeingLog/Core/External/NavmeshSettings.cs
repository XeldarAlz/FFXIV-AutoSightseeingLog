using ECommons.DalamudServices;
using System.IO;
using System.Text.Json;

namespace AutoSightseeingLog.Core.External;

// The pathfinder exposes none of its settings over IPC, so the one that breaks automation is read from its saved file.
internal static class NavmeshSettings
{
    private const string ConfigFileName = "vnavmesh.json";
    private const string PayloadProperty = "Payload";
    private const string CancelOnInputProperty = "CancelMoveOnUserInput";

    private static bool warned;

    // With the setting on, any movement key or stick input drops the path this plugin queued, so the tour stalls.
    public static void WarnIfInputCancelsMovement()
    {
        if (warned || !InputCancelsMovement())
        {
            return;
        }

        warned = true;
        Svc.Chat.PrintError($"{AslConstants.LogPrefix} vnavmesh has \"Cancel current path on player movement input\" turned on, so any movement key you press stops the tour's travel. Turn it off in vnavmesh's settings for smooth runs.");
    }

    private static bool InputCancelsMovement()
    {
        var directory = Svc.PluginInterface.ConfigFile.DirectoryName;
        if (string.IsNullOrEmpty(directory))
        {
            return false;
        }

        var path = Path.Combine(directory, ConfigFileName);
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.TryGetProperty(PayloadProperty, out var payload)
                && payload.TryGetProperty(CancelOnInputProperty, out var setting)
                && setting.ValueKind == JsonValueKind.True;
        }
        catch (Exception exception)
        {
            Svc.Log.Debug($"{AslConstants.LogPrefix} Could not read the vnavmesh settings: {exception.Message}");
            return false;
        }
    }
}
