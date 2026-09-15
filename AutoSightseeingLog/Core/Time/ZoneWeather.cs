using ECommons.DalamudServices;
using Lumina.Excel.Sheets;

namespace AutoSightseeingLog.Core.Time;

internal static class ZoneWeather
{
    // Weather rolls over every 8 bells, at 0:00, 8:00 and 16:00 Eorzea time.
    public const int BellsPerPeriod = 8;
    public const int SecondsPerPeriod = EorzeaTime.SecondsPerBell * BellsPerPeriod;

    private const uint Percent = 100;

    private static readonly Dictionary<uint, RateTable> tables = new();

    private readonly record struct RateTable(byte[] Weathers, byte[] Rates)
    {
        public static readonly RateTable Empty = new([], []);
    }

    public static byte WeatherAt(uint territoryId, long unixSeconds)
    {
        var table = TableFor(territoryId);
        var roll = Roll(unixSeconds);
        var cumulative = 0u;
        for (var slot = 0; slot < table.Rates.Length; slot++)
        {
            cumulative += table.Rates[slot];
            if (roll < cumulative)
            {
                return table.Weathers[slot];
            }
        }

        return 0;
    }

    public static long PeriodStart(long unixSeconds) => unixSeconds - unixSeconds % SecondsPerPeriod;

    // The client's own roll: a hash of the Eorzean day and the bell its next period starts at gives a percentile, and
    // the zone's rate table maps that percentile to a weather.
    private static uint Roll(long unixSeconds)
    {
        var bell = unixSeconds / EorzeaTime.SecondsPerBell;
        var nextPeriodBell = (uint)((bell + BellsPerPeriod - bell % BellsPerPeriod) % EorzeaTime.BellsPerDay);
        var day = (uint)(unixSeconds / EorzeaTime.SecondsPerDay);
        var seed = day * Percent + nextPeriodBell;
        var mixed = (seed << 11) ^ seed;
        return ((mixed >> 8) ^ mixed) % Percent;
    }

    private static RateTable TableFor(uint territoryId)
    {
        if (tables.TryGetValue(territoryId, out var cached))
        {
            return cached;
        }

        var table = Load(territoryId);
        tables[territoryId] = table;
        return table;
    }

    private static RateTable Load(uint territoryId)
    {
        if (Svc.Data.GetExcelSheet<TerritoryType>().GetRowOrDefault(territoryId)?.WeatherRate.ValueNullable is not { } rate)
        {
            return RateTable.Empty;
        }

        var count = rate.Rate.Count;
        var weathers = new byte[count];
        var rates = new byte[count];
        for (var slot = 0; slot < count; slot++)
        {
            weathers[slot] = (byte)rate.Weather[slot].RowId;
            rates[slot] = (byte)rate.Rate[slot];
        }

        return new RateTable(weathers, rates);
    }
}
