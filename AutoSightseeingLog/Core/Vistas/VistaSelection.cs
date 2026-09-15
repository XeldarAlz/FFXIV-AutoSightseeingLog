namespace AutoSightseeingLog.Core.Vistas;

internal static class VistaSelection
{
    public static VistaWorkload Measure(HashSet<ushort> selected, long now)
    {
        var vistas = VistaRegistry.All;
        var picked = 0;
        var recorded = 0;
        var open = 0;
        var waiting = 0;
        var locked = 0;
        for (var index = 0; index < vistas.Length; index++)
        {
            ref readonly var vista = ref vistas[index];
            if (!selected.Contains(vista.Number))
            {
                continue;
            }

            picked++;
            switch (VistaLog.Status(vista, now))
            {
                case VistaStatus.Done:
                    recorded++;
                    break;
                case VistaStatus.Open:
                    open++;
                    break;
                case VistaStatus.Waiting:
                    waiting++;
                    break;
                case VistaStatus.Locked:
                    locked++;
                    break;
            }
        }

        return new VistaWorkload(picked, recorded, open, waiting, locked);
    }

    // The picked vistas the run can work: not yet in the log and unlocked, in log order.
    public static ushort[] ResolvePlan(HashSet<ushort> selected, long now)
    {
        var vistas = VistaRegistry.All;
        var plan = new List<ushort>(selected.Count);
        for (var index = 0; index < vistas.Length; index++)
        {
            ref readonly var vista = ref vistas[index];
            if (!selected.Contains(vista.Number))
            {
                continue;
            }

            if (VistaLog.Status(vista, now) is VistaStatus.Open or VistaStatus.Waiting)
            {
                plan.Add(vista.Number);
            }
        }

        return [.. plan];
    }

    public static int CountSelected(HashSet<ushort> selected, ReadOnlySpan<Vista> vistas)
    {
        var count = 0;
        for (var index = 0; index < vistas.Length; index++)
        {
            if (selected.Contains(vistas[index].Number))
            {
                count++;
            }
        }

        return count;
    }
}
