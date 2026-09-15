using AutoSightseeingLog.Core.Localization;
using Dalamud.Interface;

namespace AutoSightseeingLog.Windows.Components;

internal static class StartButton
{
    public static bool Draw(string sublabel, bool enabled, string? disabledReason = null, float width = 0f)
        => HeroButton.Draw(FontAwesomeIcon.Play, Loc.T(L.Tour.Start), sublabel, Styling.AccentStar, enabled, disabledReason, width);
}
