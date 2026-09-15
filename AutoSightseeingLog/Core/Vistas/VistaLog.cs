using AutoSightseeingLog.Core.Time;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.UI;

namespace AutoSightseeingLog.Core.Vistas;

// The windows and the run ask about the log many times a frame, so it is read from the game once a second and kept here.
internal static unsafe class VistaLog
{
    private const long RefreshIntervalMs = 1_000;
    private const int BitsPerWord = 64;
    // Quest sheet row ids start at 65536, and the quest journal keys a quest by the rest.
    private const uint QuestRowBase = 0x10000;

    private static ulong[] recorded = [];
    private static bool[] questDone = [];
    private static CachedWindow[] windows = [];
    private static long refreshedAtMs = long.MinValue;
    private static bool firstLogRecorded;

    private readonly record struct CachedWindow(VistaWindow Window, bool Found, long ValidUntil);

    public static bool Loaded { get; private set; }

    public static bool FirstLogRecorded => firstLogRecorded;

    public static void Refresh(bool force = false)
    {
        var nowMs = Environment.TickCount64;
        if (!force && nowMs - refreshedAtMs < RefreshIntervalMs)
        {
            return;
        }

        refreshedAtMs = nowMs;
        var vistas = VistaRegistry.All;
        var gateQuests = VistaRegistry.GateQuests;
        EnsureCapacity(vistas.Length, gateQuests.Length);
        Array.Clear(recorded);
        Array.Clear(questDone);
        firstLogRecorded = false;

        var playerState = PlayerState.Instance();
        Loaded = Svc.ClientState.IsLoggedIn && playerState != null;
        if (!Loaded)
        {
            return;
        }

        for (var index = 0; index < vistas.Length; index++)
        {
            if (playerState->IsAdventureComplete((uint)index))
            {
                recorded[index / BitsPerWord] |= 1UL << (index % BitsPerWord);
            }
        }

        for (var gateIndex = 0; gateIndex < gateQuests.Length; gateIndex++)
        {
            var questId = gateQuests[gateIndex];
            questDone[gateIndex] = questId >= QuestRowBase && QuestManager.IsQuestComplete((ushort)(questId - QuestRowBase));
        }

        firstLogRecorded = CountRecorded(1, VistaData.FirstLogCount) == VistaData.FirstLogCount;
    }

    // Asks the game directly, for the run waiting on an emote, and updates the kept bit to match.
    public static bool CheckRecorded(ushort number)
    {
        var playerState = PlayerState.Instance();
        var index = number - 1;
        var word = index / BitsPerWord;
        if (playerState == null || index < 0 || word >= recorded.Length)
        {
            return IsRecorded(number);
        }

        if (!playerState->IsAdventureComplete((uint)index))
        {
            return false;
        }

        recorded[word] |= 1UL << (index % BitsPerWord);
        return true;
    }

    public static bool IsRecorded(ushort number)
    {
        var index = number - 1;
        var word = index / BitsPerWord;
        return index >= 0 && word < recorded.Length && (recorded[word] & (1UL << (index % BitsPerWord))) != 0;
    }

    public static bool IsUnlocked(in Vista vista) => vista.Gate switch
    {
        VistaGate.FirstLogRecorded => firstLogRecorded,
        VistaGate.Quest => IsQuestDone(vista.GateQuestId),
        _ => true,
    };

    public static bool IsQuestDone(uint questId)
    {
        var gateQuests = VistaRegistry.GateQuests;
        var count = Math.Min(gateQuests.Length, questDone.Length);
        for (var gateIndex = 0; gateIndex < count; gateIndex++)
        {
            if (gateQuests[gateIndex] == questId)
            {
                return questDone[gateIndex];
            }
        }

        return false;
    }

    public static VistaStatus Status(in Vista vista, long now)
    {
        if (!Loaded)
        {
            return VistaStatus.Unknown;
        }

        if (IsRecorded(vista.Number))
        {
            return VistaStatus.Done;
        }

        if (!IsUnlocked(vista))
        {
            return VistaStatus.Locked;
        }

        return TryGetWindow(vista, now, out var window) && window.IsOpenAt(now) ? VistaStatus.Open : VistaStatus.Waiting;
    }

    // A found window is kept until it closes; a search that found none runs again at the next weather change.
    public static bool TryGetWindow(in Vista vista, long now, out VistaWindow window)
    {
        if (windows.Length != VistaRegistry.Count)
        {
            windows = new CachedWindow[VistaRegistry.Count];
        }

        var index = vista.Number - 1;
        if (index < 0 || index >= windows.Length)
        {
            return VistaWindows.TryFindNext(vista, now, out window);
        }

        var cached = windows[index];
        if (cached.ValidUntil > now)
        {
            window = cached.Window;
            return cached.Found;
        }

        var found = VistaWindows.TryFindNext(vista, now, out window);
        var validUntil = found ? window.End : ZoneWeather.PeriodStart(now) + ZoneWeather.SecondsPerPeriod;
        windows[index] = new CachedWindow(window, found, validUntil);
        return found;
    }

    public static int CountRecorded(ReadOnlySpan<Vista> vistas)
    {
        var count = 0;
        for (var index = 0; index < vistas.Length; index++)
        {
            if (IsRecorded(vistas[index].Number))
            {
                count++;
            }
        }

        return count;
    }

    private static int CountRecorded(ushort firstNumber, ushort count)
    {
        var recordedCount = 0;
        for (var offset = 0; offset < count; offset++)
        {
            if (IsRecorded((ushort)(firstNumber + offset)))
            {
                recordedCount++;
            }
        }

        return recordedCount;
    }

    private static void EnsureCapacity(int vistaCount, int gateCount)
    {
        var words = (vistaCount + BitsPerWord - 1) / BitsPerWord;
        if (recorded.Length != words)
        {
            recorded = new ulong[words];
        }

        if (questDone.Length != gateCount)
        {
            questDone = new bool[gateCount];
        }
    }
}
