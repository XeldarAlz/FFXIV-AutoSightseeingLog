using AutoSightseeingLog.Core.External;
using AutoSightseeingLog.Core.Ipc;
using AutoSightseeingLog.Core.Travel;
using AutoSightseeingLog.Core.Vistas;
using ECommons.DalamudServices;
using System.Globalization;
using System.Numerics;
using System.Threading.Tasks;

namespace AutoSightseeingLog.Core.Tasks;

// /asl goto <number>: the travel half of a visit on its own, for checking how the plugin reaches a vista.
internal sealed class AutoGoto(ushort number) : AutoCommon
{
    private const string StopArgument = "stop";
    private const string Usage = AslConstants.LogPrefix + " Usage: /asl goto <vista number>, or /asl goto stop.";

    public static void HandleCommand(string arguments, bool tourRunning)
    {
        var automation = clib.Services.Svc.Automation;
        if (arguments.Equals(StopArgument, StringComparison.OrdinalIgnoreCase))
        {
            if (automation.CurrentTask is not AutoGoto)
            {
                Svc.Chat.Print($"{AslConstants.LogPrefix} No goto is running.");
                return;
            }

            automation.Stop();
            Svc.Chat.Print($"{AslConstants.LogPrefix} Goto stopped.");
            return;
        }

        if (!ushort.TryParse(arguments.TrimStart('#'), NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            || !VistaRegistry.TryGet(number, out var vista))
        {
            Svc.Chat.PrintError(Usage);
            return;
        }

        if (automation.CurrentTask is AutoGoto)
        {
            Svc.Chat.PrintError($"{AslConstants.LogPrefix} A goto is already running. /asl goto stop cancels it.");
            return;
        }

        if (tourRunning)
        {
            Svc.Chat.PrintError($"{AslConstants.LogPrefix} Stop the tour before using goto.");
            return;
        }

        if (!ExternalPlugins.IsInstalled(ExternalPlugin.Vnavmesh))
        {
            Svc.Chat.PrintError($"{AslConstants.LogPrefix} Goto needs the pathfinding plugin listed on the Plugins page.");
            return;
        }

        NavmeshSettings.WarnIfInputCancelsMovement();
        Svc.Chat.Print($"{AslConstants.LogPrefix} Goto: heading to #{number:000} {VistaRegistry.Name(number)} in {TerritoryNames.Of(vista.TerritoryId)}.");
        automation.Start(new AutoGoto(number));
    }

    protected override async Task Execute()
    {
        if (!VistaRegistry.TryGet(number, out var vista))
        {
            return;
        }

        var name = VistaRegistry.Name(number);
        await HoldCombatMovementAndSettle("goto");
        try
        {
            var arrived = await ReachVista(vista, $"goto#{number:000}");
            if (CancelToken.IsCancellationRequested)
            {
                Diag("Goto: cancelled.");
                return;
            }

            if (!arrived)
            {
                Svc.Chat.PrintError($"{AslConstants.LogPrefix} Goto: could not reach #{number:000} {name}. The plugin log has the details.");
                return;
            }

            Status = "Arrived";
            var player = Svc.Objects.LocalPlayer;
            var fromLogPoint = player is null ? float.NaN : Vector3.Distance(player.Position, vista.Position);
            var placement = player is not null && VistaVolumes.Of(vista).Contains(player.Position) ? "inside" : "outside";
            Svc.Chat.Print($"{AslConstants.LogPrefix} Goto: at #{number:000} {name}, {fromLogPoint:F1} yalms from its log point and {placement} its trigger volume. The log asks for {GameNames.EmoteCommand(vista.EmoteId)}.");
        }
        finally
        {
            NavmeshIPC.Instance.Stop();
            ReleaseCombatMovement("goto");
        }
    }
}
