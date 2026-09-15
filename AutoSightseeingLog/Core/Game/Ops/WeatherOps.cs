using FFXIVClientStructs.FFXIV.Client.Game;

namespace AutoSightseeingLog.Core.Game.Ops;

internal static unsafe class WeatherOps
{
    public static byte Current()
    {
        var manager = WeatherManager.Instance();
        return manager != null ? manager->GetCurrentWeather() : (byte)0;
    }
}
