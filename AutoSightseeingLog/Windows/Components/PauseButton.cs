using AutoSightseeingLog.Core.Localization;
using AutoSightseeingLog.Core.Tasks;
using Dalamud.Interface;

namespace AutoSightseeingLog.Windows.Components;

internal static class PauseButton
{
    public static bool Draw(PauseReason reason, float width = 0f) => reason switch
    {
        PauseReason.InContent => HeroButton.Draw(FontAwesomeIcon.Play, Loc.T(L.Tour.ResumeCaps), null, Styling.AccentMint, false, Loc.T(L.Tour.InContent), width),
        PauseReason.Manual    => HeroButton.Draw(FontAwesomeIcon.Play, Loc.T(L.Tour.ResumeCaps), null, Styling.AccentMint, true, null, width),
        _                     => HeroButton.Draw(FontAwesomeIcon.Pause, Loc.T(L.Tour.PauseCaps), null, Styling.AccentAmber, true, null, width),
    };
}
