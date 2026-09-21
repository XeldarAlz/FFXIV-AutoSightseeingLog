using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Interface.Utility;
using ECommons.DalamudServices;
using System.Numerics;
using ClientGameObject = FFXIVClientStructs.FFXIV.Client.Game.Object.GameObject;

namespace AutoSightseeingLog.Windows;

internal static unsafe class SpotArrow
{
    private const float ProbeMeters = 0.25f;
    private const float SquaredAxisProbesPerFacingProbe = 2f;
    private const float EndOnForeshortening = 0.08f;
    private const float MinForeshortening = 0.45f;
    private const float NameplateClearancePixels = 57f;
    private const float LengthPixels = 46f;
    private const float HeadLengthPixels = 20f;
    private const float HeadHalfWidthPixels = 13f;
    private const float ShaftHalfWidthPixels = 5f;
    private const float EndOnDotRadius = 6f;
    private const float OutlineThickness = 1.5f;
    private const float OutlineAlpha = 0.85f;

    public static void Draw(ImDrawListPtr drawList, IGameObject player, Vector3 target, Vector4 color, float size)
    {
        var head = player.Position with { Y = player.Position.Y + ((ClientGameObject*)player.Address)->Height };
        var direction = Vector3.Normalize(target - player.Position);
        if (!TryProject(head, out var headOnScreen)
            || !TryProject(head + direction * ProbeMeters, out var probeOnScreen)
            || !TryFacingProbePixels(head, headOnScreen, out var facingProbePixels))
        {
            return;
        }

        var scale = ImGuiHelpers.GlobalScale * size;
        var clearance = NameplateClearancePixels * ImGuiHelpers.GlobalScale + LengthPixels * 0.5f * scale;
        var center = headOnScreen - new Vector2(0f, clearance);
        var projected = probeOnScreen - headOnScreen;
        var foreshortening = projected.Length() / facingProbePixels;
        if (foreshortening < EndOnForeshortening)
        {
            Paint.Dot(drawList, center, EndOnDotRadius * scale, color);
            return;
        }

        var axis = Vector2.Normalize(projected);
        var side = new Vector2(-axis.Y, axis.X);
        var stretch = Math.Clamp(foreshortening, MinForeshortening, 1f) * scale;
        var tip = center + axis * (LengthPixels * 0.5f * stretch);
        var tail = center - axis * (LengthPixels * 0.5f * stretch);
        var neck = tip - axis * (HeadLengthPixels * stretch);
        var shaft = side * (ShaftHalfWidthPixels * scale);
        var wing = side * (HeadHalfWidthPixels * scale);

        var fill = Paint.Col(color);
        drawList.AddQuadFilled(tail + shaft, neck + shaft, neck - shaft, tail - shaft, fill);
        drawList.AddTriangleFilled(neck + wing, tip, neck - wing, fill);

        drawList.PathLineTo(tail + shaft);
        drawList.PathLineTo(neck + shaft);
        drawList.PathLineTo(neck + wing);
        drawList.PathLineTo(tip);
        drawList.PathLineTo(neck - wing);
        drawList.PathLineTo(neck - shaft);
        drawList.PathLineTo(tail - shaft);
        drawList.PathStroke(Paint.Col(Styling.WithAlpha(Styling.InkOnStar, OutlineAlpha)), ImDrawFlags.Closed, OutlineThickness * scale);
    }

    // However the camera is turned, probes along the three world axes cover, squared and summed, twice the square of one
    // that faces the camera, which is the length the probe toward the spot is measured against.
    private static bool TryFacingProbePixels(Vector3 origin, Vector2 originOnScreen, out float facingProbePixels)
    {
        facingProbePixels = 0f;
        if (!TryProject(origin + Vector3.UnitX * ProbeMeters, out var alongX)
            || !TryProject(origin + Vector3.UnitY * ProbeMeters, out var alongY)
            || !TryProject(origin + Vector3.UnitZ * ProbeMeters, out var alongZ))
        {
            return false;
        }

        var squaredAxisProbes = (alongX - originOnScreen).LengthSquared()
            + (alongY - originOnScreen).LengthSquared()
            + (alongZ - originOnScreen).LengthSquared();
        facingProbePixels = MathF.Sqrt(squaredAxisProbes / SquaredAxisProbesPerFacingProbe);
        return facingProbePixels > 0f;
    }

    private static bool TryProject(Vector3 world, out Vector2 screen)
        => Svc.GameGui.WorldToScreen(world, out screen, out _);
}
