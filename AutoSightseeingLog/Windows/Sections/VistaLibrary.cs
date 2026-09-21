using AutoSightseeingLog.Core;
using AutoSightseeingLog.Core.Localization;
using AutoSightseeingLog.Core.Tasks;
using AutoSightseeingLog.Core.Time;
using AutoSightseeingLog.Core.Travel;
using AutoSightseeingLog.Core.Vistas;
using AutoSightseeingLog.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using System.Numerics;

namespace AutoSightseeingLog.Windows.Sections;

internal static class VistaLibrary
{
    private const float Gap = 8f;
    private const float SummaryRowHeight = 32f;
    private const float ZoneGap = 14f;
    private const float ListSlide = 8f;
    private const float DiscRadius = 8f;
    private const int SelectedShift = 32;
    private const int RecordedShift = 16;
    private const long CountMask = 0xFFFF;

    private static readonly Segmented.Item[] segments = new Segmented.Item[ExpansionLabels.All.Length];

    private static int currentExpansion;
    private static CachedText summaryText;

    public static void Draw(Configuration configuration, TourController controller, bool scrollIntoView)
    {
        LibraryHeader.Draw(Loc.T(L.Library.Title), scrollIntoView);
        Styling.VSpace(10f);
        DrawExpansionPicker();

        using var reveal = Motion.PushSwitch("##asl_vista_list", currentExpansion, slide: ListSlide);
        var expansion = ExpansionLabels.All[currentExpansion];
        var vistas = VistaRegistry.ByZone(expansion);
        if (vistas.Length == 0)
        {
            TextDraw.Hint(Loc.T(L.Library.NoVistas));
            return;
        }

        if (!VistaLog.Loaded)
        {
            TextDraw.Hint(Loc.T(L.Library.NotLoggedIn));
            Styling.VSpace(6f);
        }

        var now = EorzeaTime.Now();
        DrawSummaryRow(configuration, controller, vistas, now);
        var zones = VistaRegistry.Zones(expansion);
        for (var zoneIndex = 0; zoneIndex < zones.Length; zoneIndex++)
        {
            var zone = zones[zoneIndex];
            GroupLabel.Draw(TerritoryNames.Of(zone.TerritoryId), zoneIndex == 0 ? 0f : ZoneGap, 6f);
            DrawGrid(vistas.Slice(zone.Start, zone.Count), configuration, controller, now);
        }
    }

    private static void DrawExpansionPicker()
    {
        for (var index = 0; index < segments.Length; index++)
        {
            segments[index] = new Segmented.Item(null, ExpansionLabels.Name(ExpansionLabels.All[index]));
        }

        Segmented.Draw("##asl_expansions", segments, ref currentExpansion);
        Styling.VSpace(8f);
    }

    private static void DrawSummaryRow(Configuration configuration, TourController controller, ReadOnlySpan<Vista> vistas, long now)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var avail = ImGui.GetContentRegionAvail().X;
        var rowHeight = SummaryRowHeight * scale;
        var summary = Summary(VistaSelection.CountSelected(configuration.SelectedVistas, vistas), VistaLog.CountRecorded(vistas), vistas.Length);

        using (Fonts.PushCaption())
        {
            var summarySize = TextDraw.Measure(summary);
            TextDraw.At(summary, new Vector2(origin.X + 2f * scale, origin.Y + (rowHeight - summarySize.Y) * 0.5f), Styling.TextDim);
        }

        var selectable = CountSelectable(vistas, now);
        var allSelected = selectable > 0 && AllSelectableSelected(configuration, vistas, now);
        var label = allSelected ? Loc.T(L.Common.Clear) : Loc.T(L.Common.SelectAll);
        var width = PillButton.Width(label);
        ImGui.SetCursorScreenPos(new Vector2(origin.X + avail - width, origin.Y));
        if (PillButton.Draw("##asl_vista_bulk", label, allSelected ? Styling.AccentRose : Styling.AccentStar,
                PillButton.Emphasis.Ghost, enabled: !controller.Running && selectable > 0, height: SummaryRowHeight))
        {
            SetAll(configuration, vistas, !allSelected, now);
            configuration.SaveDebounced();
        }

        ImGui.SetCursorScreenPos(origin);
        ImGui.Dummy(new Vector2(avail, rowHeight));
    }

    private static string Summary(int selected, int recorded, int total)
    {
        var key = (long)selected << SelectedShift | (long)recorded << RecordedShift | (total & CountMask);
        return summaryText.Get(key, static key => string.Concat(
            Loc.T(L.Library.SelectedSummary, (int)(key >> SelectedShift), (int)(key & CountMask)),
            TextDraw.Separator,
            Loc.T(L.Library.RecordedSummary, (int)((key >> RecordedShift) & CountMask), (int)(key & CountMask))));
    }

    private static void DrawGrid(ReadOnlySpan<Vista> vistas, Configuration configuration, TourController controller, long now)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var gap = Gap * scale;
        var avail = ImGui.GetContentRegionAvail().X;
        var columns = Math.Max(1, (int)MathF.Floor((avail + gap) / (Layout.VistaRowMinWidth * scale + gap)));
        var rowWidth = (avail - gap * (columns - 1)) / columns;

        using var itemSpacing = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(gap, gap));
        for (var index = 0; index < vistas.Length; index++)
        {
            if (index % columns != 0)
            {
                ImGui.SameLine(0f, gap);
            }

            DrawRow(vistas[index], configuration, controller, rowWidth, now);
        }
    }

    private static void DrawRow(in Vista vista, Configuration configuration, TourController controller, float width, long now)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var size = new Vector2(width, Layout.VistaRowHeight * scale);
        if (!ImGui.IsRectVisible(size))
        {
            ImGui.Dummy(size);
            return;
        }

        var origin = ImGui.GetCursorScreenPos();
        var end = origin + size;
        var status = VistaLog.Status(vista, now);
        var selectable = IsSelectable(status);
        var selected = configuration.SelectedVistas.Contains(vista.Number);
        var running = controller.Running;

        ImGui.PushID(vista.Number);
        var hit = Hit.Area("##vista", size, selectable && !running);
        var hover = Motion.Hover(Motion.Key("##vista"), hit.Hovered);
        var active = Motion.Approach(Motion.Key("##vista", 1), selected && selectable ? 1f : 0f, 14f);
        ImGui.PopID();

        if (hit.Clicked)
        {
            SetSelected(configuration, vista.Number, !selected);
            configuration.SaveDebounced();
        }

        var drawList = ImGui.GetWindowDrawList();
        Paint.Glass(drawList, origin, end, Styling.CardRounding * scale, Styling.AccentStar, 0.02f + 0.16f * active, hover);

        var midY = origin.Y + size.Y * 0.5f;
        var discRadius = DiscRadius * scale;
        var discCenter = new Vector2(origin.X + 13f * scale + discRadius, midY);
        DrawSelector(drawList, discCenter, discRadius, status, active);

        var rightX = end.X - 12f * scale;
        rightX -= DrawStatus(VistaText.Status(vista, status, now), rightX, midY) + 10f * scale;

        var textX = discCenter.X + discRadius + 11f * scale;
        var number = VistaText.NumberLabel(vista.Number);
        float numberWidth;
        using (Fonts.PushCaption())
        {
            var numberSize = TextDraw.Measure(number);
            numberWidth = numberSize.X;
            TextDraw.At(number, new Vector2(textX, midY - numberSize.Y * 0.5f), Styling.TextMuted);
        }

        var nameX = textX + numberWidth + 8f * scale;
        var dim = status is VistaStatus.Done or VistaStatus.Locked;
        var nameColor = dim ? Styling.TextMuted : Vector4.Lerp(Styling.TextSecondary, Styling.TextStrong, MathF.Max(active, hover));
        var name = TextDraw.Truncate(VistaRegistry.Name(vista.Number), rightX - nameX);
        var nameSize = TextDraw.Measure(name);
        TextDraw.At(name, new Vector2(nameX, midY - nameSize.Y * 0.5f), nameColor);

        if (!Hit.HoveringRect(origin, end))
        {
            return;
        }

        DrawTooltip(vista, status, now, running && selectable);
    }

    private static void DrawSelector(ImDrawListPtr drawList, Vector2 center, float radius, VistaStatus status, float active)
    {
        var scale = ImGuiHelpers.GlobalScale;
        if (status == VistaStatus.Done)
        {
            drawList.AddCircleFilled(center, radius, Paint.Col(Styling.WithAlpha(Styling.AccentMint, 0.22f)));
            Paint.Check(drawList, center, radius * 1.1f, Styling.AccentMint, 1.8f * scale);
            return;
        }

        if (status == VistaStatus.Locked)
        {
            TextDraw.IconCentered(FontAwesomeIcon.Lock, center, Styling.TextMuted);
            return;
        }

        var ring = Vector4.Lerp(Styling.WithAlpha(Styling.BorderDim, 0.9f), Styling.AccentStarSoft, active);
        drawList.AddCircle(center, radius, Paint.Col(ring), 0, 1.4f * scale);
        if (active <= 0.01f)
        {
            return;
        }

        drawList.AddCircleFilled(center, radius * active, Paint.Col(Styling.AccentStar));
        if (active > 0.5f)
        {
            Paint.Check(drawList, center, radius * 1.1f, Styling.WithAlpha(Styling.InkOnStar, (active - 0.5f) * 2f), 1.8f * scale);
        }
    }

    private static float DrawStatus(in VistaText.Visual visual, float rightX, float midY)
    {
        if (visual.Text.Length == 0)
        {
            return 0f;
        }

        var scale = ImGuiHelpers.GlobalScale;
        using (Fonts.PushCaption())
        {
            var textSize = TextDraw.Measure(visual.Text);
            var textX = rightX - textSize.X;
            TextDraw.At(visual.Text, new Vector2(textX, midY - textSize.Y * 0.5f), visual.Color);
            var iconSize = TextDraw.IconSize(visual.Icon);
            var iconX = textX - 5f * scale - iconSize.X;
            TextDraw.Icon(visual.Icon, new Vector2(iconX, midY - iconSize.Y * 0.5f), visual.Color);
            return rightX - iconX;
        }
    }

    private static void DrawTooltip(in Vista vista, VistaStatus status, long now, bool lockedByRun)
    {
        using (Tooltip.Begin())
        {
            Tooltip.Text(VistaRegistry.Name(vista.Number), Styling.TextStrong);
            Tooltip.Text(TerritoryNames.Of(vista.TerritoryId), Styling.TextDim);

            var emote = GameNames.EmoteCommand(vista.EmoteId);
            if (emote.Length > 0)
            {
                Tooltip.Text(Loc.T(L.Library.TooltipEmote, emote), Styling.TextSecondary);
            }

            if (vista.HasTimeWindow)
            {
                Tooltip.Text(VistaText.TimeWindow(vista), Styling.TextSecondary);
            }

            if (vista.WeatherMask != 0)
            {
                Tooltip.Text(Loc.T(L.Library.TooltipWeather, VistaText.Weathers(vista.WeatherMask)), Styling.TextSecondary);
            }

            var (line, color) = StatusLine(vista, status, now);
            if (line.Length > 0)
            {
                Tooltip.Text(line, color);
            }

            var approach = ApproachLine(vista);
            if (approach.Length > 0)
            {
                Tooltip.Text(approach, Styling.AccentAmberSoft);
            }

            if (lockedByRun)
            {
                Tooltip.Text(Loc.T(L.Library.LockedRunning), Styling.TextMuted);
            }
        }
    }

    private static (string Line, Vector4 Color) StatusLine(in Vista vista, VistaStatus status, long now)
    {
        switch (status)
        {
            case VistaStatus.Done:
                return (Loc.T(L.Library.TooltipDone), Styling.AccentMint);
            case VistaStatus.Locked:
                return (LockLine(vista), Styling.AccentAmber);
            case VistaStatus.Open:
                return VistaLog.TryGetWindow(vista, now, out var open) && !open.IsEndless
                    ? (Loc.T(L.Library.TooltipOpenFor, VistaText.Duration(open.End - now)), Styling.AccentStar)
                    : (Loc.T(L.Library.TooltipOpenAlways), Styling.AccentStar);
            case VistaStatus.Waiting:
                return VistaLog.TryGetWindow(vista, now, out var next)
                    ? (Loc.T(L.Library.TooltipOpensIn, VistaText.Duration(next.Start - now)), Styling.AccentAmberSoft)
                    : (Loc.T(L.Library.TooltipNoWindow), Styling.TextDim);
            default:
                return (string.Empty, Styling.TextDim);
        }
    }

    private static string LockLine(in Vista vista) => vista.Gate switch
    {
        VistaGate.Quest => Loc.T(L.Library.LockedQuest, GameNames.Quest(vista.GateQuestId)),
        VistaGate.FirstLogRecorded => Loc.T(L.Library.LockedFirstLog, GameNames.Npc(VistaData.SecondLogNpcId), TerritoryNames.Of(VistaData.SecondLogTerritoryId)),
        _ => string.Empty,
    };

    private static string ApproachLine(in Vista vista) => vista.Approach switch
    {
        VistaApproach.JumpPuzzle => Loc.T(JumpRoutes.Has(vista.Number) ? L.Library.TooltipJumpRoute : L.Library.TooltipJumpPuzzle),
        VistaApproach.NpcGate    => Loc.T(L.Library.TooltipNpcGate),
        VistaApproach.Indoors    => Loc.T(L.Library.TooltipIndoors),
        VistaApproach.Ledge      => Loc.T(L.Library.TooltipLedge),
        _                        => string.Empty,
    };

    // Before login the log cannot be read, so every vista can be picked and the run sorts out what is left.
    private static bool IsSelectable(VistaStatus status) => status is VistaStatus.Open or VistaStatus.Waiting or VistaStatus.Unknown;

    private static int CountSelectable(ReadOnlySpan<Vista> vistas, long now)
    {
        var count = 0;
        for (var index = 0; index < vistas.Length; index++)
        {
            if (IsSelectable(VistaLog.Status(vistas[index], now)))
            {
                count++;
            }
        }

        return count;
    }

    private static bool AllSelectableSelected(Configuration configuration, ReadOnlySpan<Vista> vistas, long now)
    {
        for (var index = 0; index < vistas.Length; index++)
        {
            if (!IsSelectable(VistaLog.Status(vistas[index], now)))
            {
                continue;
            }

            if (!configuration.SelectedVistas.Contains(vistas[index].Number))
            {
                return false;
            }
        }

        return true;
    }

    private static void SetSelected(Configuration configuration, ushort number, bool selected)
    {
        if (selected)
        {
            configuration.SelectedVistas.Add(number);
            return;
        }

        configuration.SelectedVistas.Remove(number);
    }

    private static void SetAll(Configuration configuration, ReadOnlySpan<Vista> vistas, bool selected, long now)
    {
        for (var index = 0; index < vistas.Length; index++)
        {
            if (selected && !IsSelectable(VistaLog.Status(vistas[index], now)))
            {
                continue;
            }

            SetSelected(configuration, vistas[index].Number, selected);
        }
    }
}
