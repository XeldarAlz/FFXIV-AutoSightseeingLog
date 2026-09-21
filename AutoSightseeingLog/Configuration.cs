using AutoSightseeingLog.Core;
using Dalamud.Configuration;
using ECommons.Throttlers;
using Newtonsoft.Json;

namespace AutoSightseeingLog;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    [JsonIgnore]
    private bool savePending;

    public int Version { get; set; }

    public bool AutoShowOnLogin { get; set; } = false;

    public string Language { get; set; } = "";

    // Sightseeing Log numbers, 1 onward, as the log itself shows them.
    public HashSet<ushort> SelectedVistas { get; set; } = [];

    // Never fires on a manual Stop or a fault.
    public AfterRunAction AfterRun { get; set; } = AfterRunAction.StayLoggedIn;

    public bool AutoPauseInContent { get; set; } = true;

    public bool AskAtHardSpots { get; set; } = true;

    public SpotMarkerVisibility BoxVisibility { get; set; } = SpotMarkerVisibility.Always;

    public SpotMarkerVisibility ArrowVisibility { get; set; } = SpotMarkerVisibility.Always;

    public int ArrowSizePercent { get; set; } = 200;

    public void Save()
    {
        savePending = false;
        Plugin.PluginInterface.SavePluginConfig(this);
    }

    // A slider or a text box changes on every frame it is edited, so the throttle drops most calls; the change stays
    // pending, and FlushPendingSave writes the last one once the throttle window has passed.
    public void SaveDebounced()
    {
        savePending = true;
        FlushPendingSave();
    }

    public void FlushPendingSave()
    {
        if (savePending && EzThrottler.Throttle(AslConstants.ThrottleKeys.Save, AslConstants.SaveThrottleMs))
        {
            Save();
        }
    }

    public void SaveIfPending()
    {
        if (savePending)
        {
            Save();
        }
    }
}
