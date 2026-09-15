namespace AutoSightseeingLog.Core.Vistas;

internal readonly record struct VistaWorkload(int Selected, int Recorded, int Open, int Waiting, int Locked)
{
    public int Pending => Open + Waiting;
}
