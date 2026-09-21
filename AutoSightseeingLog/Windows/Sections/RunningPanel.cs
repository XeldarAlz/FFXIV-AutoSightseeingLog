using AutoSightseeingLog.Core.Localization;
using AutoSightseeingLog.Core.Tasks;
using AutoSightseeingLog.Core.Time;
using AutoSightseeingLog.Core.Travel;
using AutoSightseeingLog.Core.Vistas;
using AutoSightseeingLog.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Utility;
using ECommons.DalamudServices;
using System.Numerics;

namespace AutoSightseeingLog.Windows.Sections;

internal static class RunningPanel
{
    private const float PadX = 18f;
    private const int QueueLength = 6;

    private static uint cachedTerritoryId = uint.MaxValue;
    private static string cachedZoneName = string.Empty;
    private static ushort cachedVista;
    private static string cachedVistaLine = string.Empty;

    public static void Draw(TourController controller)
    {
        var paused = controller.Paused;
        var (accent, accentSoft, label) = PhasePalette(controller);
        var session = controller.SessionSnapshot;
        var planned = session?.Plan.Count ?? 0;
        var logged = session?.VistasLogged ?? 0;
        var now = EorzeaTime.Now();

        DrawHeaderStrip(planned, accent, accentSoft, paused);
        Styling.VSpace(6f);
        DrawHeroCard(controller, logged, planned, accent, accentSoft, label);

        Styling.VSpace(10f);
        DrawStatTiles(session, controller.Progress, now);

        Styling.VSpace(10f);
        DrawQueue(controller, now);
    }

    private static void DrawHeaderStrip(int planned, Vector4 accent, Vector4 accentSoft, bool paused)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var origin = ImGui.GetCursorScreenPos();
        var avail = ImGui.GetContentRegionAvail().X;
        var lineHeight = ImGui.GetTextLineHeight();
        var midY = origin.Y + lineHeight * 0.5f;

        var dot = paused ? accent : Styling.PulseColor(accent, accentSoft, Styling.PulseMedium);
        var radius = 4f * scale;
        Paint.Dot(drawList, new Vector2(origin.X + radius + 3f * scale, midY), radius, dot);

        var status = paused ? Loc.T(L.Shell.StatusPaused) : Loc.T(L.Shell.StatusRunning);
        var statusSize = TextDraw.SmallCapsSize(status);
        TextDraw.SmallCaps(status, new Vector2(origin.X + radius * 2f + 12f * scale, midY - statusSize.Y * 0.5f), Styling.TextSecondary);

        var footer = Loc.Plural(L.Run.InPlay, planned, CurrentZoneName());
        using (Fonts.PushCaption())
        {
            var footerSize = TextDraw.Measure(footer);
            TextDraw.At(footer, new Vector2(origin.X + avail - footerSize.X, midY - footerSize.Y * 0.5f), Styling.TextMuted);
        }

        ImGui.Dummy(new Vector2(avail, lineHeight));
    }

    private static void DrawHeroCard(TourController controller, int logged, int planned, Vector4 accent, Vector4 accentSoft, string label)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var size = new Vector2(ImGui.GetContentRegionAvail().X, Layout.HeroCardHeight * scale);
        var origin = ImGui.GetCursorScreenPos();
        var end = origin + size;
        var drawList = ImGui.GetWindowDrawList();
        var active = controller.Running && !controller.Paused;
        var rounding = Styling.PanelRounding * scale;

        Paint.Glass(drawList, origin, end, rounding, accent, active ? 0.10f : 0.03f, 0f, elevated: true);
        if (active)
        {
            Paint.Stroke(drawList, origin, end, Styling.PulseColor(Styling.WithAlpha(accent, 0.5f), accentSoft, Styling.PulseMedium), rounding, 1.6f);
        }

        var padX = PadX * scale;
        var ringRadius = size.Y * 0.5f - 18f * scale;
        var ringCenter = new Vector2(origin.X + padX + ringRadius, origin.Y + size.Y * 0.5f);
        DrawRing(ringCenter, ringRadius, accent, active, logged, planned);

        var columnX = ringCenter.X + ringRadius + 20f * scale;
        var columnWidth = end.X - padX - columnX;
        var y = origin.Y + 16f * scale;

        y += DrawPhaseChip(columnX, y, label, accent, accentSoft) + 10f * scale;

        var vistaLine = CurrentVistaLine(controller.Progress.CurrentVista);
        if (vistaLine.Length > 0)
        {
            var vista = TextDraw.Truncate(vistaLine, columnWidth);
            TextDraw.At(vista, new Vector2(columnX, y), Styling.TextStrong);
            y += TextDraw.Measure(vista).Y + 4f * scale;
        }

        var status = TextDraw.Truncate(string.IsNullOrWhiteSpace(controller.Status) ? Loc.T(L.Common.Working) : controller.Status, columnWidth);
        var statusSize = TextDraw.Measure(status);
        TextDraw.At(status, new Vector2(columnX, y), Styling.TextSecondary);
        y += statusSize.Y + 12f * scale;

        var barHeight = 8f * scale;
        var barOrigin = new Vector2(columnX, y);
        if (active && planned > 0)
        {
            var fraction = Motion.Approach(Motion.Key("##asl_vista_bar"), logged / (float)planned, 10f);
            Paint.Bar(drawList, barOrigin, columnWidth, barHeight, fraction, accent);
        }
        else if (active)
        {
            Paint.IndeterminateBar(drawList, barOrigin, columnWidth, barHeight, accent);
        }
        else
        {
            Paint.Bar(drawList, barOrigin, columnWidth, barHeight, 0f, accent);
        }

        y += barHeight + 8f * scale;
        using (Fonts.PushCaption())
        {
            var left = planned - logged;
            var remaining = left > 0 ? Loc.Plural(L.Run.VistasToGo, left) : Loc.T(L.Run.AllRecorded);
            TextDraw.At(remaining, new Vector2(columnX, y), Styling.WithAlpha(accentSoft, 0.9f));
        }

        ImGui.Dummy(size);
    }

    private static void DrawRing(Vector2 center, float radius, Vector4 accent, bool active, int logged, int planned)
    {
        var thickness = 6f * ImGuiHelpers.GlobalScale;
        ProgressRing.Track(center, radius, thickness, Styling.WithAlpha(Styling.BorderDim, 0.7f));

        if (planned == 0)
        {
            if (active)
            {
                ProgressRing.Sweep(center, radius, thickness, accent, Styling.PulseOrbit, MathF.PI * 0.6f, 1f);
            }

            ProgressRing.CenterIcon(center, FontAwesomeIcon.Binoculars, Styling.TextDim, radius * 0.55f);
            return;
        }

        var fraction = Motion.Approach(Motion.Key("##asl_vista_ring"), logged / (float)planned, 6f);
        ProgressRing.Fill(center, radius, thickness, fraction, accent);
        ProgressRing.CenterValue(center, NumberText.Of(logged), Loc.T(L.Run.GoalOf, planned), Styling.TextStrong, Styling.TextDim);
    }

    private static float DrawPhaseChip(float x, float y, string text, Vector4 accent, Vector4 accentSoft)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var drawList = ImGui.GetWindowDrawList();
        var padX = 9f * scale;
        var padY = 3f * scale;

        using (Fonts.PushCaption())
        {
            var label = TextDraw.Upper(text);
            var textSize = TextDraw.Measure(label);
            var chipMin = new Vector2(x, y);
            var chipMax = chipMin + new Vector2(padX * 2f + textSize.X, textSize.Y + padY * 2f);
            Paint.Pill(drawList, chipMin, chipMax, Styling.WithAlpha(accent, 0.28f), Styling.WithAlpha(accent, 0.65f));
            TextDraw.At(label, new Vector2(x + padX, y + padY), accentSoft);
            return chipMax.Y - chipMin.Y;
        }
    }

    private static (Vector4 Accent, Vector4 AccentSoft, string Label) PhasePalette(TourController controller)
    {
        if (!controller.Running)
        {
            return (Styling.TextDim, Styling.TextSecondary, Loc.T(L.Run.PhaseReady));
        }

        if (controller.Paused)
        {
            return controller.PauseReason == PauseReason.InContent
                ? (Styling.AccentAmber, Styling.AccentAmberSoft, Loc.T(L.Run.PhasePausedInContent))
                : (Styling.AccentAmber, Styling.AccentAmberSoft, Loc.T(L.Run.PhasePaused));
        }

        return controller.Phase switch
        {
            TourPhase.Idle      => (Styling.TextDim, Styling.TextSecondary, Loc.T(L.Run.PhaseStandingBy)),
            TourPhase.Finishing => (Styling.AccentMint, Styling.AccentMintSoft, Loc.T(L.Run.PhaseFinishing)),
            _                   => (Styling.AccentBlue, Styling.AccentBlueSoft, ReadyState.PhaseLabel(controller.Phase)),
        };
    }

    private static void DrawStatTiles(TourSession? session, TourProgress progress, long now)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var gap = 8f * scale;
        var tileWidth = (ImGui.GetContentRegionAvail().X - gap * 3f) / 4f;

        StatTile.Draw(Loc.T(L.Run.TileVistas), NumberText.Of(session?.VistasLogged ?? 0), null, Styling.AccentStar, tileWidth);
        ImGui.SameLine(0, gap);
        StatTile.Draw(Loc.T(L.Run.TileZones), NumberText.Of(session?.ZonesVisited ?? 0), null, Styling.AccentMint, tileWidth);
        ImGui.SameLine(0, gap);
        StatTile.Draw(Loc.T(L.Run.TileOpen), NumberText.Of(CountOpen(progress.Queue, now)), null, Styling.AccentAmber, tileWidth);
        ImGui.SameLine(0, gap);
        StatTile.Draw(Loc.T(L.Run.TileElapsed), Formatting.Elapsed(session?.Elapsed ?? TimeSpan.Zero), null, Styling.AccentBlue, tileWidth);
    }

    private static int CountOpen(ReadOnlySpan<ushort> queue, long now)
    {
        var open = 0;
        for (var index = 0; index < queue.Length; index++)
        {
            if (VistaRegistry.TryGet(queue[index], out var vista) && VistaLog.Status(vista, now) == VistaStatus.Open)
            {
                open++;
            }
        }

        return open;
    }

    private static void DrawQueue(TourController controller, long now)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var origin = ImGui.GetCursorScreenPos();
        var heading = Loc.T(L.Run.UpNext);
        var labelSize = TextDraw.SectionTitleSize(heading);
        TextDraw.SectionTitle(heading, origin, Styling.TextStrong);
        ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, labelSize.Y + 8f * scale));

        var queue = controller.Progress.Queue;
        if (queue.Length == 0)
        {
            TextDraw.Hint(controller.Phase == TourPhase.Reading ? Loc.T(L.Run.QueueFirst) : Loc.T(L.Run.NothingLeft));
            return;
        }

        var shown = 0;
        for (var index = 0; index < queue.Length && shown < QueueLength; index++)
        {
            if (!VistaRegistry.TryGet(queue[index], out var vista) || VistaLog.IsRecorded(vista.Number))
            {
                continue;
            }

            DrawQueueRow(vista, now, shown == 0);
            shown++;
        }

        if (shown == 0)
        {
            TextDraw.Hint(Loc.T(L.Run.NothingLeft));
        }
    }

    private static void DrawQueueRow(in Vista vista, long now, bool emphasize)
    {
        var scale = ImGuiHelpers.GlobalScale;
        var size = new Vector2(ImGui.GetContentRegionAvail().X, Layout.QueueRowHeight * scale);
        var origin = ImGui.GetCursorScreenPos();
        var end = origin + size;
        var drawList = ImGui.GetWindowDrawList();
        var visual = VistaText.Status(vista, VistaLog.Status(vista, now), now);

        Paint.Glass(drawList, origin, end, Styling.CardRounding * scale, visual.Color, emphasize ? 0.10f : 0.03f);

        var padX = 13f * scale;
        var midY = origin.Y + size.Y * 0.5f;
        var iconSize = TextDraw.IconSize(FontAwesomeIcon.Binoculars);
        TextDraw.Icon(FontAwesomeIcon.Binoculars, new Vector2(origin.X + padX, midY - iconSize.Y * 0.5f), emphasize ? visual.Color : Styling.TextDim);

        var meta = string.Concat(TerritoryNames.Of(vista.TerritoryId), TextDraw.Separator, visual.Text);
        Vector2 metaSize;
        using (Fonts.PushCaption())
        {
            metaSize = TextDraw.Measure(meta);
            TextDraw.At(meta, new Vector2(end.X - padX - metaSize.X, midY - metaSize.Y * 0.5f), Styling.TextDim);
        }

        var nameX = origin.X + padX + iconSize.X + 10f * scale;
        var name = TextDraw.Truncate(VistaRegistry.Name(vista.Number), end.X - padX - metaSize.X - 12f * scale - nameX);
        var nameSize = TextDraw.Measure(name);
        TextDraw.At(name, new Vector2(nameX, midY - nameSize.Y * 0.5f), emphasize ? Styling.TextStrong : Styling.TextSecondary);

        ImGui.Dummy(size);
    }

    private static string CurrentVistaLine(ushort number)
    {
        if (number == cachedVista)
        {
            return cachedVistaLine;
        }

        cachedVista = number;
        cachedVistaLine = number == 0 ? string.Empty : string.Concat(VistaText.NumberLabel(number), "  ", VistaRegistry.Name(number));
        return cachedVistaLine;
    }

    private static string CurrentZoneName()
    {
        uint territoryId = Svc.ClientState.TerritoryType;
        if (territoryId == cachedTerritoryId)
        {
            return cachedZoneName;
        }

        cachedTerritoryId = territoryId;
        cachedZoneName = TerritoryNames.Of(territoryId);
        if (cachedZoneName.Length == 0)
        {
            cachedZoneName = Loc.T(L.Run.SomewhereElse);
        }

        return cachedZoneName;
    }
}
