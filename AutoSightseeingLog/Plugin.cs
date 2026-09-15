using AutoSightseeingLog.Core;
using AutoSightseeingLog.Core.Debug;
using AutoSightseeingLog.Core.Game.Watchers;
using AutoSightseeingLog.Core.Localization;
using AutoSightseeingLog.Core.Stats;
using AutoSightseeingLog.Core.Tasks;
using AutoSightseeingLog.Windows;
using AutoSightseeingLog.Windows.Shell;
using clib;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons;
using ECommons.DalamudServices;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace AutoSightseeingLog;

public sealed class Plugin : IDalamudPlugin
{
    private const string GotoSubcommand = "goto";
    private const string NavmeshIpcProviderMarker = "Navmesh.IPCProvider";

    [PluginService]
    internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;

    [PluginService]
    internal static ICommandManager CommandManager { get; private set; } = null!;

    internal static Plugin Instance { get; private set; } = null!;

    internal Configuration Configuration { get; }
    internal WindowSystem WindowSystem { get; } = new("AutoSightseeingLog");
    internal RunHistory History { get; }
    internal TourController Controller { get; }

    private readonly DutyWatcher dutyWatcher;
    private readonly AppWindow appWindow;
    private readonly CommandInfo primaryCommand;
    private readonly CommandInfo aliasCommand;

    public Plugin()
    {
        Instance = this;

        ECommonsMain.Init(PluginInterface, this);
        CLibMain.Init(PluginInterface, this, CLibModule.Automation);

        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        History = new RunHistory();
        Controller = new TourController();
        dutyWatcher = new DutyWatcher();

        InitializeLocalization();
        Fonts.Initialize(PluginInterface.UiBuilder, PluginDirectory);
        appWindow = new AppWindow(this);
        WindowSystem.AddWindow(appWindow);

        primaryCommand = new CommandInfo(OnCommand) { HelpMessage = Loc.T(L.Plugin.CommandHelp) };
        aliasCommand = new CommandInfo(OnCommand) { HelpMessage = Loc.T(L.Plugin.CommandHelpAlias) };
        CommandManager.AddHandler(AslConstants.PrimaryCommand, primaryCommand);
        CommandManager.AddHandler(AslConstants.AliasCommand, aliasCommand);

        PluginInterface.UiBuilder.Draw += OnDraw;
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUi;

        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        Svc.Framework.Update += OnFrameworkUpdate;
        Svc.ClientState.Login += OnLogin;
        if (Svc.ClientState.IsLoggedIn)
        {
            OnLogin();
        }
    }

    private static string PluginDirectory => PluginInterface.AssemblyLocation.DirectoryName ?? string.Empty;

    public void Dispose()
    {
        Controller.Stop();

        PluginInterface.UiBuilder.Draw -= OnDraw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleMainUi;
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        Svc.Framework.Update -= OnFrameworkUpdate;
        Svc.ClientState.Login -= OnLogin;
        Configuration.SaveIfPending();

        WindowSystem.RemoveAllWindows();
        appWindow.Dispose();
        Fonts.Dispose();

        CommandManager.RemoveHandler(AslConstants.PrimaryCommand);
        CommandManager.RemoveHandler(AslConstants.AliasCommand);

        dutyWatcher.Dispose();

        CLibMain.Dispose();
        ECommonsMain.Dispose();
    }

    public void OnLanguageChanged()
    {
        primaryCommand.HelpMessage = Loc.T(L.Plugin.CommandHelp);
        aliasCommand.HelpMessage = Loc.T(L.Plugin.CommandHelpAlias);
    }

    public void ToggleMainUi() => appWindow.Toggle();

    public void ToggleConfigUi() => appWindow.TogglePage(AppWindow.Page.Settings);

    public void ToggleAboutUi() => appWindow.TogglePage(AppWindow.Page.About);

    public void ToggleDependenciesUi() => appWindow.TogglePage(AppWindow.Page.Plugins);

    public void ToggleHistoryUi() => appWindow.TogglePage(AppWindow.Page.History);

    private void OnCommand(string command, string args)
    {
        var trimmed = args.Trim();
        if (trimmed.Equals("config", StringComparison.OrdinalIgnoreCase))
        {
            ToggleConfigUi();
        }
        else if (trimmed.Equals("about", StringComparison.OrdinalIgnoreCase))
        {
            ToggleAboutUi();
        }
        else if (trimmed.Equals("deps", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("dependencies", StringComparison.OrdinalIgnoreCase))
        {
            ToggleDependenciesUi();
        }
        else if (trimmed.Equals("stats", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("history", StringComparison.OrdinalIgnoreCase))
        {
            ToggleHistoryUi();
        }
        else if (trimmed.Equals("pause", StringComparison.OrdinalIgnoreCase) || trimmed.Equals("resume", StringComparison.OrdinalIgnoreCase))
        {
            Controller.TogglePause();
        }
        else if (trimmed.Equals("logdump", StringComparison.OrdinalIgnoreCase))
        {
            VistaDumper.Dump();
        }
        else if (IsGotoCommand(trimmed))
        {
            AutoGoto.HandleCommand(trimmed[GotoSubcommand.Length..].Trim(), Controller.Running);
        }
        else
        {
            ToggleMainUi();
        }
    }

    private static bool IsGotoCommand(string arguments)
        => arguments.StartsWith(GotoSubcommand, StringComparison.OrdinalIgnoreCase)
        && (arguments.Length == GotoSubcommand.Length || char.IsWhiteSpace(arguments[GotoSubcommand.Length]));

    // The navmesh plugin answers pathfind IPC on fire-and-forget tasks this plugin never gets a handle to. When one
    // faults, typically a query issued while the zone mesh is still building, the finalizer would rethrow it as noise.
    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs eventArgs)
    {
        if (eventArgs.Observed)
        {
            return;
        }

        if (!eventArgs.Exception.ToString().Contains(NavmeshIpcProviderMarker, StringComparison.Ordinal))
        {
            return;
        }

        eventArgs.SetObserved();
        Svc.Log.Debug($"{AslConstants.LogPrefix} Observed a navmesh IPC task fault: {eventArgs.Exception.GetBaseException().Message}");
    }

    private void OnDraw()
    {
        WindowSystem.Draw();
        Configuration.FlushPendingSave();
    }

    private void OnFrameworkUpdate(IFramework framework) => Controller.Tick();

    private void InitializeLocalization()
    {
        var directory = Path.Combine(PluginDirectory, "Localization");
        if (string.IsNullOrEmpty(Configuration.Language))
        {
            Configuration.Language = DetectLanguage();
            Configuration.Save();
        }

        Loc.Initialize(Configuration.Language, directory);
    }

    private static string DetectLanguage()
    {
        var dalamudLanguage = PluginInterface.UiLanguage;
        if (Languages.IsKnown(dalamudLanguage))
        {
            return Languages.Resolve(dalamudLanguage).Code;
        }

        switch (Svc.ClientState.ClientLanguage)
        {
            case Dalamud.Game.ClientLanguage.German: return Languages.German.Code;
            case Dalamud.Game.ClientLanguage.French: return Languages.French.Code;
            case Dalamud.Game.ClientLanguage.Japanese: return Languages.Japanese.Code;
        }

        var osLanguage = CultureInfo.InstalledUICulture.TwoLetterISOLanguageName;
        return Languages.IsKnown(osLanguage) ? Languages.Resolve(osLanguage).Code : Languages.English.Code;
    }

    private void OnLogin()
    {
        if (!Configuration.AutoShowOnLogin)
        {
            return;
        }

        appWindow.Show(AppWindow.Page.Vistas);
    }
}
