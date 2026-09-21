using AutoSightseeingLog.Core.Vistas;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.Utility;
using ECommons.DalamudServices;
using System.Numerics;

namespace AutoSightseeingLog.Windows;

internal static class WorldOverlay
{
    private const float DrawRangeMeters = 60f;
    private const int CornerCount = 8;
    private const int RingSegments = 24;
    private const int RingSpokeEvery = 6;
    private const float EdgeThickness = 2f;
    private const float FootingDotRadius = 5f;
    private const float PercentToFraction = 0.01f;
    private const long ArrowPreviewMs = 1_200;
    private const float PreviewRise = 0.6f;

    private static readonly (int From, int To)[] boxEdges =
    [
        (0, 1), (1, 3), (3, 2), (2, 0),
        (4, 5), (5, 7), (7, 6), (6, 4),
        (0, 4), (1, 5), (2, 6), (3, 7),
    ];

    private static long arrowPreviewUntil;

    public static void PreviewArrow() => arrowPreviewUntil = Environment.TickCount64 + ArrowPreviewMs;

    public static void Draw()
    {
        if (Svc.Objects.LocalPlayer is not { } player)
        {
            return;
        }

        var configuration = Plugin.Instance.Configuration;
        var arrowSize = configuration.ArrowSizePercent * PercentToFraction;
        var arrowDrawn = VistaSpot.Active
            && Svc.ClientState.TerritoryType == VistaSpot.TerritoryId
            && DrawMarkers(configuration, player, arrowSize);
        if (!arrowDrawn && Environment.TickCount64 < arrowPreviewUntil)
        {
            SpotArrow.Draw(ImGui.GetBackgroundDrawList(), player, PreviewTarget(player), Styling.AccentStar, arrowSize);
        }
    }

    private static bool DrawMarkers(Configuration configuration, IGameObject player, float arrowSize)
    {
        var volume = VistaSpot.Volume;
        var near = Vector3.Distance(player.Position, volume.Center) <= DrawRangeMeters;
        var inside = volume.Contains(player.Position);
        var showBox = IsShown(configuration.BoxVisibility, near);
        var showArrow = !inside && IsShown(configuration.ArrowVisibility, near);
        if (!showBox && !showArrow)
        {
            return false;
        }

        var drawList = ImGui.GetBackgroundDrawList();
        var edgeColor = EdgeColor(inside);
        if (showBox)
        {
            DrawSpot(drawList, volume, Paint.Col(edgeColor));
        }

        if (showArrow)
        {
            SpotArrow.Draw(drawList, player, volume.Center, edgeColor, arrowSize);
        }

        return showArrow;
    }

    private static Vector3 PreviewTarget(IGameObject player)
        => player.Position + new Vector3(MathF.Sin(player.Rotation), PreviewRise, MathF.Cos(player.Rotation));

    private static bool IsShown(SpotMarkerVisibility visibility, bool near)
        => visibility switch
        {
            SpotMarkerVisibility.Always => true,
            SpotMarkerVisibility.NearSpot => near || VistaSpot.AwaitingPlayer,
            SpotMarkerVisibility.WhenAsked => VistaSpot.AwaitingPlayer,
            _ => false,
        };

    private static void DrawSpot(ImDrawListPtr drawList, in VistaVolume volume, uint color)
    {
        if (volume.IsCylinder)
        {
            DrawCylinder(drawList, volume, color);
        }
        else
        {
            DrawBox(drawList, volume, color);
        }

        if (VistaSpot.HasFooting && Svc.GameGui.WorldToScreen(VistaSpot.Footing, out var footing))
        {
            Paint.Dot(drawList, footing, FootingDotRadius * ImGuiHelpers.GlobalScale, Styling.AccentMint);
        }
    }

    private static Vector4 EdgeColor(bool inside)
    {
        if (inside)
        {
            return Styling.AccentMint;
        }

        return VistaSpot.AwaitingPlayer
            ? Styling.PulseColor(Styling.AccentStar, Styling.AccentRose, Styling.PulseFast)
            : Styling.AccentStar;
    }

    private static void DrawBox(ImDrawListPtr drawList, in VistaVolume volume, uint color)
    {
        Span<Vector3> corners = stackalloc Vector3[CornerCount];
        for (var corner = 0; corner < CornerCount; corner++)
        {
            corners[corner] = volume.ToWorld(new Vector3(
                (corner & 1) == 0 ? -volume.HalfExtents.X : volume.HalfExtents.X,
                (corner & 4) == 0 ? -volume.HalfExtents.Y : volume.HalfExtents.Y,
                (corner & 2) == 0 ? -volume.HalfExtents.Z : volume.HalfExtents.Z));
        }

        for (var edge = 0; edge < boxEdges.Length; edge++)
        {
            DrawEdge(drawList, corners[boxEdges[edge].From], corners[boxEdges[edge].To], color);
        }
    }

    private static void DrawCylinder(ImDrawListPtr drawList, in VistaVolume volume, uint color)
    {
        var previous = RingPoint(volume, 0);
        for (var segment = 1; segment <= RingSegments; segment++)
        {
            var current = RingPoint(volume, segment);
            DrawEdge(drawList, previous with { Y = volume.Top }, current with { Y = volume.Top }, color);
            DrawEdge(drawList, previous with { Y = volume.Bottom }, current with { Y = volume.Bottom }, color);
            if (segment % RingSpokeEvery == 0)
            {
                DrawEdge(drawList, current with { Y = volume.Top }, current with { Y = volume.Bottom }, color);
            }

            previous = current;
        }
    }

    private static Vector3 RingPoint(in VistaVolume volume, int segment)
    {
        var angle = MathF.Tau * segment / RingSegments;
        return volume.ToWorld(new Vector3(MathF.Cos(angle) * volume.HalfExtents.X, 0f, MathF.Sin(angle) * volume.HalfExtents.Z));
    }

    private static void DrawEdge(ImDrawListPtr drawList, Vector3 from, Vector3 to, uint color)
    {
        if (Svc.GameGui.WorldToScreen(from, out var screenFrom) && Svc.GameGui.WorldToScreen(to, out var screenTo))
        {
            drawList.AddLine(screenFrom, screenTo, color, EdgeThickness * ImGuiHelpers.GlobalScale);
        }
    }
}
