using AutoSightseeingLog.Core;
using AutoSightseeingLog.Core.Localization;
using AutoSightseeingLog.Core.Vistas;
using Dalamud.Interface;
using System.Globalization;
using System.Numerics;
using System.Text;

namespace AutoSightseeingLog.Windows;

// Countdowns are rebuilt only when their minute changes (their second, in the last minute), so the log drawn every
// frame does not format them again.
internal static class VistaText
{
    private const long SecondsPerMinute = 60;
    private const long SecondsPerHour = 3_600;
    private const int KindShift = 40;
    private const long UnitMask = (1L << KindShift) - 1;
    private const int BellsPerDay = 24;
    private const int WeatherBits = 32;

    private static readonly Dictionary<uint, string> weatherLists = new();

    private static CachedText[] countdowns = [];
    private static string[] numberLabels = [];

    public readonly record struct Visual(FontAwesomeIcon Icon, string Text, Vector4 Color);

    private enum Countdown : byte
    {
        Left = 1,
        In = 2,
    }

    public static Visual Status(in Vista vista, VistaStatus status, long now)
    {
        switch (status)
        {
            case VistaStatus.Done:
                return new Visual(FontAwesomeIcon.Check, Loc.T(L.Library.StatusDone), Styling.AccentMint);
            case VistaStatus.Locked:
                return new Visual(FontAwesomeIcon.Lock, Loc.T(L.Library.StatusLocked), Styling.TextMuted);
            case VistaStatus.Open:
                if (VistaLog.TryGetWindow(vista, now, out var open) && !open.IsEndless)
                {
                    return new Visual(FontAwesomeIcon.Eye, CountdownText(vista.Number, Countdown.Left, open.End - now), Styling.AccentSky);
                }

                return new Visual(FontAwesomeIcon.Eye, Loc.T(L.Library.StatusOpen), Styling.AccentSky);
            case VistaStatus.Waiting:
                if (VistaLog.TryGetWindow(vista, now, out var next))
                {
                    return new Visual(FontAwesomeIcon.Clock, CountdownText(vista.Number, Countdown.In, next.Start - now), Styling.AccentAmber);
                }

                return new Visual(FontAwesomeIcon.Clock, Loc.T(L.Library.StatusNoWindow), Styling.TextDim);
            default:
                return new Visual(FontAwesomeIcon.QuestionCircle, string.Empty, Styling.TextMuted);
        }
    }

    public static string Duration(long seconds)
    {
        seconds = Math.Max(0, seconds);
        if (seconds >= SecondsPerHour)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{seconds / SecondsPerHour}h {seconds % SecondsPerHour / SecondsPerMinute:00}m");
        }

        return seconds >= SecondsPerMinute
            ? string.Create(CultureInfo.InvariantCulture, $"{seconds / SecondsPerMinute}m")
            : string.Create(CultureInfo.InvariantCulture, $"{seconds}s");
    }

    // The log's windows end on the 59th minute, so the closing hour shown is the bell after the last one.
    public static string TimeWindow(in Vista vista)
        => Loc.T(L.Library.TooltipTime, BellLabel(vista.FirstBell), BellLabel((vista.LastBell + 1) % BellsPerDay));

    public static string Weathers(uint mask)
    {
        if (weatherLists.TryGetValue(mask, out var cached))
        {
            return cached;
        }

        var builder = new StringBuilder();
        for (var weatherId = 1u; weatherId < WeatherBits; weatherId++)
        {
            if ((mask & (1u << (int)weatherId)) == 0)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.Append(", ");
            }

            builder.Append(GameNames.Weather(weatherId));
        }

        var list = builder.ToString();
        weatherLists[mask] = list;
        return list;
    }

    public static string NumberLabel(ushort number)
    {
        if (number >= numberLabels.Length)
        {
            var grown = new string[Math.Max(number + 1, numberLabels.Length * 2)];
            Array.Copy(numberLabels, grown, numberLabels.Length);
            for (var index = numberLabels.Length; index < grown.Length; index++)
            {
                grown[index] = string.Create(CultureInfo.InvariantCulture, $"#{index:000}");
            }

            numberLabels = grown;
        }

        return numberLabels[number];
    }

    private static string BellLabel(int bell) => string.Create(CultureInfo.InvariantCulture, $"{bell:00}:00");

    private static string CountdownText(ushort number, Countdown kind, long seconds)
    {
        if (countdowns.Length != VistaRegistry.Count)
        {
            countdowns = new CachedText[VistaRegistry.Count];
        }

        var index = number - 1;
        seconds = Math.Max(0, seconds);
        var unit = seconds < SecondsPerMinute ? seconds : SecondsPerMinute + seconds / SecondsPerMinute;
        var key = (long)kind << KindShift | unit;
        if (index < 0 || index >= countdowns.Length)
        {
            return Build(key);
        }

        return countdowns[index].Get(key, static key => Build(key));
    }

    private static string Build(long key)
    {
        var unit = key & UnitMask;
        var seconds = unit < SecondsPerMinute ? unit : (unit - SecondsPerMinute) * SecondsPerMinute;
        var duration = Duration(seconds);
        return (Countdown)(key >> KindShift) == Countdown.Left
            ? Loc.T(L.Library.StatusLeft, duration)
            : Loc.T(L.Library.StatusIn, duration);
    }
}
