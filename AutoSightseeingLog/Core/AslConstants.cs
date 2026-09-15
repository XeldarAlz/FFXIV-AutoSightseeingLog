namespace AutoSightseeingLog.Core;

internal static class AslConstants
{
    public const string PrimaryCommand = "/asl";
    public const string AliasCommand = "/sightseeing";

    public const string LogPrefix = "[ASL]";

    public const int SaveThrottleMs = 500;

    internal static class ThrottleKeys
    {
        public const string Save = "AutoSightseeingLog.Save";
    }
}
