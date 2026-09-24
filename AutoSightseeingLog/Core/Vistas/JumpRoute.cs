namespace AutoSightseeingLog.Core.Vistas;

internal readonly record struct JumpRoute(JumpStep[] Steps, bool Recorded)
{
    private const float RecordedArriveMeters = 0.1f;
    private const float ScriptedArriveMeters = 0.25f;

    public int Length => Steps.Length;

    public float ArriveMeters => Recorded ? RecordedArriveMeters : ScriptedArriveMeters;

    public JumpStep this[int leg] => Steps[leg];
}
