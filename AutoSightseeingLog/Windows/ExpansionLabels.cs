using AutoSightseeingLog.Core.Localization;
using AutoSightseeingLog.Core.Vistas;

namespace AutoSightseeingLog.Windows;

internal static class ExpansionLabels
{
    public static readonly ExpansionKind[] All =
    [
        ExpansionKind.ARealmReborn, ExpansionKind.Heavensward, ExpansionKind.Stormblood,
        ExpansionKind.Shadowbringers, ExpansionKind.Endwalker, ExpansionKind.Dawntrail,
    ];

    public static string Name(ExpansionKind kind) => kind switch
    {
        ExpansionKind.ARealmReborn   => Loc.T(L.Library.ExpansionArr),
        ExpansionKind.Heavensward    => Loc.T(L.Library.ExpansionHw),
        ExpansionKind.Stormblood     => Loc.T(L.Library.ExpansionSb),
        ExpansionKind.Shadowbringers => Loc.T(L.Library.ExpansionShb),
        ExpansionKind.Endwalker      => Loc.T(L.Library.ExpansionEw),
        _                            => Loc.T(L.Library.ExpansionDt),
    };

    // The community's short forms, the same in every language.
    public static string Tag(ExpansionKind kind) => kind switch
    {
        ExpansionKind.ARealmReborn   => "ARR",
        ExpansionKind.Heavensward    => "HW",
        ExpansionKind.Stormblood     => "SB",
        ExpansionKind.Shadowbringers => "ShB",
        ExpansionKind.Endwalker      => "EW",
        _                            => "DT",
    };
}
