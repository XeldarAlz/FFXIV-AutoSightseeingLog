using AutoSightseeingLog.Core.Time;

namespace AutoSightseeingLog.Core.Vistas;

internal static class VistaWindows
{
    // 60 Eorzean days, about 70 Earth hours. A vista whose next window lies further out reads as having none in sight,
    // and the search runs again at the next weather change.
    private const int SearchBells = EorzeaTime.BellsPerDay * 60;
    private const int WeatherBits = 32;

    public static bool TryFindNext(in Vista vista, long now, out VistaWindow window)
    {
        if (!vista.HasConditions)
        {
            window = VistaWindow.Always;
            return true;
        }

        var firstBellStart = EorzeaTime.BellStart(now);
        for (var bell = 0; bell < SearchBells; bell++)
        {
            var start = firstBellStart + (long)bell * EorzeaTime.SecondsPerBell;
            if (!Holds(vista, start))
            {
                continue;
            }

            var end = start + EorzeaTime.SecondsPerBell;
            for (var extended = bell + 1; extended < SearchBells && Holds(vista, end); extended++)
            {
                end += EorzeaTime.SecondsPerBell;
            }

            window = new VistaWindow(start, end);
            return true;
        }

        window = default;
        return false;
    }

    private static bool Holds(in Vista vista, long unixSeconds)
    {
        if (vista.HasTimeWindow && !EorzeaTime.InBells(EorzeaTime.BellOf(unixSeconds), vista.FirstBell, vista.LastBell))
        {
            return false;
        }

        if (vista.WeatherMask == 0)
        {
            return true;
        }

        var weather = ZoneWeather.WeatherAt(vista.TerritoryId, unixSeconds);
        return weather < WeatherBits && (vista.WeatherMask & (1u << weather)) != 0;
    }
}
