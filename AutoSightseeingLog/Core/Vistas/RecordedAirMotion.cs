namespace AutoSightseeingLog.Core.Vistas;

internal static class RecordedAirMotion
{
    // The game's jumping flag includes walking off a ledge. Require an observed rise before
    // encoding a jump action; otherwise replaying a drop can jump straight into an overhang.
    public static bool IsDrop(float takeoffHeight, float apexHeight, float landingHeight)
        => apexHeight <= takeoffHeight + 0.15f && landingHeight < takeoffHeight - 0.3f;
}
