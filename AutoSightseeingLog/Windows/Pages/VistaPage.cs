using AutoSightseeingLog.Core.Vistas;
using AutoSightseeingLog.Windows.Sections;
using AutoSightseeingLog.Windows.Shell;

namespace AutoSightseeingLog.Windows.Pages;

internal sealed class VistaPage
{
    private const float SwitchRevealMs = 320f;

    private bool scrollToLibrary;

    public void Draw(Plugin plugin, AppWindow window)
    {
        VistaLog.Refresh();
        var configuration = plugin.Configuration;
        var controller = plugin.Controller;
        var running = controller.Running;

        using var reveal = Motion.PushSwitch("##asl_vista_state", running, SwitchRevealMs);
        if (running)
        {
            RunningPanel.Draw(controller);
            return;
        }

        if (Headline.Draw(configuration, controller, plugin.History))
        {
            window.Show(AppWindow.Page.Plugins);
        }

        Styling.VSpace(20f);
        if (PlanCard.Draw(configuration, controller))
        {
            scrollToLibrary = true;
        }

        Styling.VSpace(26f);
        VistaLibrary.Draw(configuration, controller, scrollToLibrary);
        scrollToLibrary = false;
        Styling.VSpace(12f);
    }
}
