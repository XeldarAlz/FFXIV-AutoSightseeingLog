using Dalamud.Interface.ImGuiNotification;
using Dalamud.Utility;
using ECommons.DalamudServices;

namespace AutoSightseeingLog.Core.Game.Ops;

internal static class AttentionOps
{
    public static IActiveNotification Ask(string title, string content, TimeSpan waitingFor)
    {
        Util.FlashWindow();
        return Svc.NotificationManager.AddNotification(new Notification
        {
            Title = title,
            Content = content,
            Type = NotificationType.Warning,
            InitialDuration = waitingFor,
            Minimized = false,
        });
    }
}
