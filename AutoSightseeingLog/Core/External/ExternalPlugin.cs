using ECommons.DalamudServices;

namespace AutoSightseeingLog.Core.External;

public enum ExternalPlugin
{
    Vnavmesh,
    Questionable,
}

public sealed record ExternalPluginInfo(
    string InternalName,
    string DisplayName,
    string RepoUrl,
    string Purpose,
    bool Required,
    // Alternate InternalNames (community forks with the same IPC surface).
    string[]? Aliases = null);

public static class ExternalPlugins
{
    private static readonly ExternalPlugin[] all = [ExternalPlugin.Vnavmesh, ExternalPlugin.Questionable];

    public static readonly IReadOnlyDictionary<ExternalPlugin, ExternalPluginInfo> Catalog
        = new Dictionary<ExternalPlugin, ExternalPluginInfo>
    {
        [ExternalPlugin.Vnavmesh] = new(
            InternalName: "vnavmesh",
            DisplayName: "vnavmesh",
            RepoUrl: "https://puni.sh/api/repository/veyn",
            Purpose: "Pathfinding, flying, and movement to every vista.",
            Required: true),
        [ExternalPlugin.Questionable] = new(
            InternalName: "Questionable",
            DisplayName: "Questionable",
            RepoUrl: "https://love.puni.sh/ment.json",
            Purpose: "Completes the quest that unlocks the Sightseeing Log on a character that has not done it yet.",
            Required: false),
    };

    public static IReadOnlyList<ExternalPlugin> All => all;

    public static bool IsInstalled(ExternalPlugin plugin)
    {
        var info = Catalog[plugin];
        foreach (var installed in Svc.PluginInterface.InstalledPlugins)
        {
            if (!installed.IsLoaded)
            {
                continue;
            }

            if (installed.InternalName == info.InternalName)
            {
                return true;
            }

            if (info.Aliases is not null && Array.IndexOf(info.Aliases, installed.InternalName) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    public static bool AllRequiredInstalled()
    {
        for (var index = 0; index < all.Length; index++)
        {
            if (Catalog[all[index]].Required && !IsInstalled(all[index]))
            {
                return false;
            }
        }

        return true;
    }

    public static string MissingRequiredNames()
    {
        var missing = new List<string>(all.Length);
        for (var index = 0; index < all.Length; index++)
        {
            var info = Catalog[all[index]];
            if (info.Required && !IsInstalled(all[index]))
            {
                missing.Add(info.DisplayName);
            }
        }

        return string.Join(", ", missing);
    }
}
