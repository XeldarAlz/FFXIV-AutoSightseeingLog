using AutoSightseeingLog.Core.Localization;
using AutoSightseeingLog.Windows.Components;
using Dalamud.Bindings.ImGui;

namespace AutoSightseeingLog.Windows.Sections.Config;

internal static class GeneralSettings
{
    private const int ArrowSizeMinPercent = 50;
    private const int ArrowSizeMaxPercent = 200;
    private const string PercentFormat = "%d%%";

    // Ordered like SpotMarkerVisibility, because the enum value indexes this array.
    private static readonly SettingsControls.Choices.Choice[] markerVisibilityChoices =
    [
        new(L.Settings.MarkerAlways, L.Settings.MarkerAlwaysDetail),
        new(L.Settings.MarkerNear, L.Settings.MarkerNearDetail),
        new(L.Settings.MarkerAsked, L.Settings.MarkerAskedDetail),
        new(L.Settings.MarkerNever, L.Settings.MarkerNeverDetail),
    ];

    public static void Draw(Configuration configuration)
    {
        DrawLanguageGroup(configuration);
        DrawWindowGroup(configuration);
        DrawBehaviorGroup(configuration);
        DrawMarkersGroup(configuration);
    }

    private static void DrawLanguageGroup(Configuration configuration)
    {
        using var group = SettingsGroup.Begin(Loc.T(L.Settings.Language));

        SettingsRow.Draw(Loc.T(L.Settings.Language),
            Loc.T(L.Settings.LanguageHelp),
            SettingsControls.RowComboWidth,
            () => SettingsControls.DrawLanguageCombo(configuration));
    }

    private static void DrawWindowGroup(Configuration configuration)
    {
        using var group = SettingsGroup.Begin(Loc.T(L.Settings.GeneralWindow));

        SettingsRow.Draw(Loc.T(L.Settings.OpenOnLogin),
            Loc.T(L.Settings.OpenOnLoginHelp),
            SettingsControls.ToggleWidth,
            () => SettingsControls.DrawToggle(configuration, () => configuration.AutoShowOnLogin, value => configuration.AutoShowOnLogin = value, "##asl_general_autoshow"),
            SettingsRow.ToggleHeight);
    }

    private static void DrawBehaviorGroup(Configuration configuration)
    {
        using var group = SettingsGroup.Begin(Loc.T(L.Settings.GeneralBehavior));

        SettingsRow.Draw(Loc.T(L.Settings.AutoPause),
            Loc.T(L.Settings.AutoPauseHelp),
            SettingsControls.ToggleWidth,
            () => SettingsControls.DrawToggle(configuration, () => configuration.AutoPauseInContent, value => configuration.AutoPauseInContent = value, "##asl_general_autopause"),
            SettingsRow.ToggleHeight);

        SettingsRow.Draw(Loc.T(L.Settings.AskAtHardSpots),
            Loc.T(L.Settings.AskAtHardSpotsHelp),
            SettingsControls.ToggleWidth,
            () => SettingsControls.DrawToggle(configuration, () => configuration.AskAtHardSpots, value => configuration.AskAtHardSpots = value, "##asl_general_askathardspots"),
            SettingsRow.ToggleHeight);
    }

    private static void DrawMarkersGroup(Configuration configuration)
    {
        using var group = SettingsGroup.Begin(Loc.T(L.Settings.Markers));

        SettingsRow.Draw(Loc.T(L.Settings.MarkerBox),
            Loc.T(L.Settings.MarkerBoxHelp),
            SettingsControls.RowComboWidth,
            () => DrawVisibilityCombo(configuration, "##asl_markers_box", configuration.BoxVisibility, value => configuration.BoxVisibility = value));

        SettingsRow.Draw(Loc.T(L.Settings.MarkerArrow),
            Loc.T(L.Settings.MarkerArrowHelp),
            SettingsControls.RowComboWidth,
            () => DrawVisibilityCombo(configuration, "##asl_markers_arrow", configuration.ArrowVisibility, value => configuration.ArrowVisibility = value));

        SettingsRow.Draw(Loc.T(L.Settings.MarkerArrowSize),
            Loc.T(L.Settings.MarkerArrowSizeHelp),
            SettingsControls.RowSliderWidth,
            () => DrawArrowSizeSlider(configuration));
    }

    private static void DrawVisibilityCombo(Configuration configuration, string id, SpotMarkerVisibility current, Action<SpotMarkerVisibility> setter)
        => SettingsControls.Choices.DrawCombo(id, markerVisibilityChoices, (int)current, picked =>
        {
            setter((SpotMarkerVisibility)picked);
            configuration.SaveDebounced();
        });

    private static void DrawArrowSizeSlider(Configuration configuration)
    {
        SettingsControls.DrawIntSlider(configuration, "##asl_markers_arrowsize", () => configuration.ArrowSizePercent, value => configuration.ArrowSizePercent = value,
            ArrowSizeMinPercent, ArrowSizeMaxPercent, PercentFormat);
        if (ImGui.IsItemHovered() || ImGui.IsItemActive())
        {
            WorldOverlay.PreviewArrow();
        }
    }
}
