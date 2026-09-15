using FFXIVClientStructs.FFXIV.Client.System.Framework;

namespace AutoSightseeingLog.Core.Time;

internal static class EorzeaTime
{
    // A bell, one Eorzean hour, lasts 175 Earth seconds.
    public const int SecondsPerBell = 175;
    public const int BellsPerDay = 24;
    public const int SecondsPerDay = SecondsPerBell * BellsPerDay;

    private const int MinutesPerBell = 60;

    // Server time keeps the clock in step with the game's even when the PC clock drifts.
    public static long Now()
    {
        var serverTime = Framework.GetServerTime();
        return serverTime > 0 ? serverTime : DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    public static int BellOf(long unixSeconds) => (int)(unixSeconds / SecondsPerBell % BellsPerDay);

    public static int MinuteOf(long unixSeconds) => (int)(unixSeconds % SecondsPerBell * MinutesPerBell / SecondsPerBell);

    public static long BellStart(long unixSeconds) => unixSeconds - unixSeconds % SecondsPerBell;

    // A window whose last bell comes before its first one runs past midnight.
    public static bool InBells(int bell, byte firstBell, byte lastBell)
        => firstBell <= lastBell ? bell >= firstBell && bell <= lastBell : bell >= firstBell || bell <= lastBell;
}
