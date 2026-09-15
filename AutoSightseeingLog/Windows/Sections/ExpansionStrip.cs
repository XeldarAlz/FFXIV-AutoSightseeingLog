using AutoSightseeingLog.Core.Localization;
using AutoSightseeingLog.Core.Tasks;
using AutoSightseeingLog.Core.Vistas;
using AutoSightseeingLog.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using System.Numerics;

namespace AutoSightseeingLog.Windows.Sections;

internal static class ExpansionStrip
{
    private const float Gap = 8f;
    private const float PadX = 11f;
    private const float InnerGap = 6f;

    private static readonly int[] counts = new int[ExpansionLabels.All.Length];

    private readonly record struct ChipMetrics(float Total, float BodyWidth, string Tag, float TagWidth, string Count, float CountWidth);

    public static void Draw(Configuration configuration, TourController controller, float maxWidth)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var regionStart = ImGui.GetCursorScreenPos();
        if (CountPicked(configuration) == 0)
        {
            using (Fonts.PushCaption())
            {
                var hint = Loc.T(L.Plan.Hint);
                TextDraw.At(hint, new Vector2(regionStart.X + 2f * scale, regionStart.Y), Styling.TextMuted);
                ImGui.Dummy(new Vector2(maxWidth, TextDraw.Measure(hint).Y));
            }

            return;
        }

        var running = controller.Running;
        var height = Layout.ChipHeight * scale;
        var gap = Gap * scale;
        var x = regionStart.X;
        var y = regionStart.Y;
        var bottom = y + height;
        var remove = -1;

        for (var index = 0; index < counts.Length; index++)
        {
            if (counts[index] == 0)
            {
                continue;
            }

            var metrics = Measure(ExpansionLabels.All[index], counts[index]);
            if (x > regionStart.X && x + metrics.Total > regionStart.X + maxWidth)
            {
                x = regionStart.X;
                y += height + gap;
            }

            if (DrawChip(new Vector2(x, y), height, metrics, index, running))
            {
                remove = index;
            }

            x += metrics.Total + gap;
            bottom = MathF.Max(bottom, y + height);
        }

        ImGui.SetCursorScreenPos(regionStart);
        ImGui.Dummy(new Vector2(maxWidth, bottom - regionStart.Y));

        if (remove < 0)
        {
            return;
        }

        Deselect(configuration, ExpansionLabels.All[remove]);
        configuration.SaveDebounced();
    }

    // A recorded vista stays picked but has nothing left to do, so only the rest are counted.
    private static int CountPicked(Configuration configuration)
    {
        var total = 0;
        for (var index = 0; index < counts.Length; index++)
        {
            var vistas = VistaRegistry.ByZone(ExpansionLabels.All[index]);
            var count = 0;
            for (var vistaIndex = 0; vistaIndex < vistas.Length; vistaIndex++)
            {
                var number = vistas[vistaIndex].Number;
                if (configuration.SelectedVistas.Contains(number) && !VistaLog.IsRecorded(number))
                {
                    count++;
                }
            }

            counts[index] = count;
            total += count;
        }

        return total;
    }

    private static void Deselect(Configuration configuration, ExpansionKind expansion)
    {
        var vistas = VistaRegistry.ByZone(expansion);
        for (var index = 0; index < vistas.Length; index++)
        {
            configuration.SelectedVistas.Remove(vistas[index].Number);
        }
    }

    private static ChipMetrics Measure(ExpansionKind expansion, int count)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var padX = PadX * scale;
        var gap = InnerGap * scale;
        var tag = ExpansionLabels.Tag(expansion);
        var countText = NumberText.Of(count);
        float tagWidth;
        using (Fonts.PushCaption())
        {
            tagWidth = TextDraw.Measure(tag).X;
        }

        var countWidth = TextDraw.Measure(countText).X;
        var bodyWidth = padX + tagWidth + gap + countWidth + gap;
        var closeWidth = TextDraw.IconSize(FontAwesomeIcon.Times).X + gap * 2f;
        return new ChipMetrics(bodyWidth + closeWidth, bodyWidth, tag, tagWidth, countText, countWidth);
    }

    private static bool DrawChip(Vector2 origin, float height, ChipMetrics metrics, int expansionIndex, bool running)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var end = origin + new Vector2(metrics.Total, height);

        ImGui.PushID(expansionIndex);
        ImGui.SetCursorScreenPos(new Vector2(origin.X + metrics.BodyWidth, origin.Y));
        var closeClicked = ImGui.InvisibleButton("##close", new Vector2(metrics.Total - metrics.BodyWidth, height));
        var closeHovered = ImGui.IsItemHovered();
        var hover = Motion.Hover(Motion.Key("##chip"), !running && Hit.HoveringRect(origin, end));
        ImGui.PopID();
        if (!running && closeHovered)
        {
            ImGui.SetMouseCursor(ImGuiMouseCursor.Hand);
        }

        var accent = Styling.AccentSky;
        var rounding = height * 0.5f;
        var tint = running ? 0.06f : 0.24f + 0.14f * hover;
        Paint.Gradient(drawList, origin, end, Styling.Tint(Styling.Surface2, accent, tint), Styling.Tint(Styling.Surface1, accent, tint * 0.8f), rounding);
        Paint.TopLight(drawList, origin, end, rounding, 0.09f);
        Paint.Stroke(drawList, origin, end, running ? Styling.WithAlpha(Styling.BorderDim, 0.6f) : Styling.WithAlpha(accent, 0.45f + 0.35f * hover), rounding);

        var midY = origin.Y + height * 0.5f;
        var cursorX = origin.X + PadX * scale;
        var gap = InnerGap * scale;

        using (Fonts.PushCaption())
        {
            var tagSize = TextDraw.Measure(metrics.Tag);
            TextDraw.At(metrics.Tag, new Vector2(cursorX, midY - tagSize.Y * 0.5f), running ? Styling.TextMuted : Styling.TextDim);
        }

        cursorX += metrics.TagWidth + gap;
        var countSize = TextDraw.Measure(metrics.Count);
        TextDraw.At(metrics.Count, new Vector2(cursorX, midY - countSize.Y * 0.5f), running ? Styling.TextDim : Styling.TextStrong);

        var closeColor = running ? Styling.TextMuted : closeHovered ? Styling.AccentRose : Styling.TextDim;
        var closeSize = TextDraw.IconSize(FontAwesomeIcon.Times);
        TextDraw.Icon(FontAwesomeIcon.Times, new Vector2(origin.X + metrics.BodyWidth + gap, midY - closeSize.Y * 0.5f), closeColor);

        if (!running && closeHovered)
        {
            Tooltip.Show(Loc.T(L.Plan.RemoveFromPlan));
        }
        else if (Hit.HoveringRect(origin, end))
        {
            Tooltip.Show(ExpansionLabels.Name(ExpansionLabels.All[expansionIndex]));
        }

        return !running && closeClicked;
    }
}
