namespace AutoSightseeingLog.Core.Vistas;

internal enum VistaApproach : byte
{
    Open,
    Indoors,
    // The log's point sits on geometry nobody can land on, so the vista is recorded from a nearby ledge.
    Ledge,
    JumpPuzzle,
    NpcGate,
}
