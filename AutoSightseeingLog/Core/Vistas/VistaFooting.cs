using AutoSightseeingLog.Core.Ipc;
using AutoSightseeingLog.Core.Travel;
using FFXIVClientStructs.FFXIV.Common.Component.BGCollision;
using System.Numerics;

namespace AutoSightseeingLog.Core.Vistas;

internal readonly record struct Footing(Vector3 Point, Vector3 WalkFrom, float Margin, bool Walkable, int Candidates);

internal record struct FootingProbe(int Rays, int Hits, int TooSteep, int OutsideHeight);

internal static class VistaFooting
{
    public const float WalkMaxAcrossMeters = 2f;
    public const float WalkMaxRiseMeters = 0.3f;
    public const float WalkMaxDropMeters = 1f;

    private const int GridSteps = 4;
    private const int ColumnsPerAxis = GridSteps * 2 + 1;
    private const int MaxLayersPerColumn = 4;
    private const int MaxCandidates = ColumnsPerAxis * ColumnsPerAxis * MaxLayersPerColumn;
    private const int MaxReachChecks = 48;
    private const float EdgeInsetFraction = 0.9f;
    private const float MinimumVerticalMarginMeters = 0.01f;
    private const float WalkableNormalY = 0.6f;
    private const float RayStartLiftMeters = 0.02f;
    private const float NextLayerDropMeters = 0.1f;
    private const float WalkSearchHalfExtentMeters = 2f;
    private const float FloorProbeLiftMeters = 1f;
    private const float FloorProbeReachMeters = 3f;
    private const float SightLineLiftMeters = 0.5f;
    private const float SightLineMinMeters = 0.1f;
    private const float SufficientMarginMeters = 0.5f;
    private const float AcrossPenaltyPerMeter = 0.1f;

    private static readonly Vector3 down = new(0f, -1f, 0f);

    private readonly record struct Candidate(Vector3 Point, float Margin);

    public static bool TryFind(in VistaVolume volume, out Footing footing, out FootingProbe probe)
    {
        Span<Candidate> candidates = stackalloc Candidate[MaxCandidates];
        probe = default;
        var count = CollectCandidates(volume, candidates, ref probe);
        if (count == 0)
        {
            footing = default;
            return false;
        }

        var found = candidates[..count];
        var deepest = Deepest(found);
        footing = PickWalkable(found) ?? new Footing(deepest.Point, default, deepest.Margin, false, count);
        return true;
    }

    private static int CollectCandidates(in VistaVolume volume, Span<Candidate> candidates, ref FootingProbe probe)
    {
        var count = 0;
        for (var stepX = -GridSteps; stepX <= GridSteps; stepX++)
        {
            for (var stepZ = -GridSteps; stepZ <= GridSteps; stepZ++)
            {
                var fractionX = stepX / (float)GridSteps;
                var fractionZ = stepZ / (float)GridSteps;
                if (volume.IsCylinder && fractionX * fractionX + fractionZ * fractionZ > 1f)
                {
                    continue;
                }

                var column = volume.ToWorld(new Vector3(
                    fractionX * volume.HalfExtents.X * EdgeInsetFraction,
                    0f,
                    fractionZ * volume.HalfExtents.Z * EdgeInsetFraction));
                count = CollectColumn(volume, column, candidates, count, ref probe);
            }
        }

        return count;
    }

    private static int CollectColumn(in VistaVolume volume, Vector3 column, Span<Candidate> candidates, int count, ref FootingProbe probe)
    {
        var startY = volume.Top + RayStartLiftMeters;
        for (var layer = 0; layer < MaxLayersPerColumn && count < candidates.Length; layer++)
        {
            var reach = startY - volume.Bottom;
            if (reach <= 0f)
            {
                break;
            }

            probe.Rays++;
            if (!BGCollisionModule.RaycastMaterialFilter(column with { Y = startY }, down, out var hit, reach))
            {
                break;
            }

            probe.Hits++;
            if (!(hit.Normal.Y >= WalkableNormalY))
            {
                probe.TooSteep++;
            }
            else if (volume.VerticalMargin(hit.Point) < MinimumVerticalMarginMeters)
            {
                probe.OutsideHeight++;
            }
            else
            {
                candidates[count++] = new Candidate(hit.Point, volume.HorizontalMargin(hit.Point));
            }

            startY = hit.Point.Y - NextLayerDropMeters;
        }

        return count;
    }

    private static Footing? PickWalkable(Span<Candidate> candidates)
    {
        var navmesh = NavmeshIPC.Instance;
        var checks = Math.Min(candidates.Length, MaxReachChecks);
        Footing? best = null;
        var bestScore = float.MinValue;
        for (var rank = 0; rank < checks; rank++)
        {
            MoveDeepestToFront(candidates[rank..]);
            var candidate = candidates[rank];
            if (best is not null && candidate.Margin < SufficientMarginMeters)
            {
                break;
            }

            if (!TryWalkFrom(navmesh, candidate.Point, out var walkFrom))
            {
                continue;
            }

            var score = MathF.Min(candidate.Margin, SufficientMarginMeters) - AcrossPenaltyPerMeter * GroundDistance.Between(walkFrom, candidate.Point);
            if (score <= bestScore)
            {
                continue;
            }

            bestScore = score;
            best = new Footing(candidate.Point, walkFrom, candidate.Margin, true, candidates.Length);
        }

        return best;
    }

    private static bool TryWalkFrom(NavmeshIPC navmesh, Vector3 point, out Vector3 walkFrom)
    {
        walkFrom = default;
        if (navmesh.NearestPointReachable(point, WalkSearchHalfExtentMeters, WalkMaxDropMeters) is not { } onMesh)
        {
            return false;
        }

        var floor = FloorUnder(onMesh);
        var rise = point.Y - floor.Y;
        if (GroundDistance.Between(floor, point) > WalkMaxAcrossMeters || rise > WalkMaxRiseMeters || -rise > WalkMaxDropMeters)
        {
            return false;
        }

        if (!HasClearLine(floor, point))
        {
            return false;
        }

        walkFrom = floor;
        return true;
    }

    private static Vector3 FloorUnder(Vector3 onMesh)
        => BGCollisionModule.RaycastMaterialFilter(onMesh with { Y = onMesh.Y + FloorProbeLiftMeters }, down, out var hit, FloorProbeReachMeters)
            ? hit.Point
            : onMesh;

    private static bool HasClearLine(Vector3 from, Vector3 to)
    {
        var lift = new Vector3(0f, SightLineLiftMeters, 0f);
        var line = to - from;
        var length = line.Length();
        return length < SightLineMinMeters
            || !BGCollisionModule.RaycastMaterialFilter(from + lift, line / length, out _, length);
    }

    private static Candidate Deepest(Span<Candidate> candidates)
    {
        var deepest = candidates[0];
        for (var index = 1; index < candidates.Length; index++)
        {
            if (candidates[index].Margin > deepest.Margin)
            {
                deepest = candidates[index];
            }
        }

        return deepest;
    }

    private static void MoveDeepestToFront(Span<Candidate> candidates)
    {
        var deepestIndex = 0;
        for (var index = 1; index < candidates.Length; index++)
        {
            if (candidates[index].Margin > candidates[deepestIndex].Margin)
            {
                deepestIndex = index;
            }
        }

        (candidates[0], candidates[deepestIndex]) = (candidates[deepestIndex], candidates[0]);
    }
}
