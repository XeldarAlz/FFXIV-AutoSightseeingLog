using System.Collections.Frozen;
using System.Numerics;

namespace AutoSightseeingLog.Core.Vistas;

// Facts the game data does not carry: the weather each A Realm Reborn vista asks for, and reachable points near the
// vistas whose log position sits where the pathfinder cannot go. Their sources are credited in THIRD-PARTY-NOTICES.md.
internal static class VistaData
{
    // "Sights of the North"; AdventureExPhase lists the unlock quests from Stormblood onward only.
    public const uint HeavenswardQuestId = 67643;

    public const ushort FirstLogCount = 20;

    // Millith Ironheart in Old Gridania opens the rest of the A Realm Reborn log once the first 20 are recorded.
    public const uint SecondLogNpcId = 1009297;
    public const uint SecondLogTerritoryId = 133;

    // "A Sight to Behold", from Naoh Gamduhla in New Gridania, hands over the Sightseeing Log; nothing records before it.
    public const uint UnlockQuestId = 65698;
    public const uint UnlockQuestNpcId = 1000384;
    public const uint UnlockQuestTerritoryId = 132;

    // "Sylph-management", the main scenario quest the log's quest waits on.
    public const uint UnlockQuestPrerequisiteId = 66049;

    // Bits are Weather sheet row ids.
    private const uint Clear = 1u << 1;
    private const uint Fair = 1u << 2;
    private const uint Clouds = 1u << 3;
    private const uint Fog = 1u << 4;
    private const uint Gales = 1u << 6;
    private const uint Rain = 1u << 7;
    private const uint Showers = 1u << 8;
    private const uint Thunder = 1u << 9;
    private const uint Thunderstorms = 1u << 10;
    private const uint DustStorms = 1u << 11;
    private const uint HeatWaves = 1u << 14;
    private const uint Snow = 1u << 15;
    private const uint Blizzards = 1u << 16;
    private const uint Gloom = 1u << 17;
    private const uint ClearOrFair = Clear | Fair;
    private const uint RainOrShowers = Rain | Showers;

    // Indexed by log number minus one. Every A Realm Reborn vista asks for a weather; no later vista does.
    private static readonly uint[] realmRebornWeathers =
    [
        ClearOrFair, ClearOrFair, RainOrShowers, ClearOrFair, Clouds, ClearOrFair, Fog, ClearOrFair, Clouds, ClearOrFair,
        ClearOrFair, ClearOrFair, ClearOrFair, ClearOrFair, Clouds, ClearOrFair, Fog, RainOrShowers, Clouds, ClearOrFair,
        ClearOrFair, ClearOrFair, RainOrShowers, ClearOrFair, RainOrShowers, ClearOrFair, Gales, ClearOrFair, ClearOrFair, ClearOrFair,
        ClearOrFair, Thunderstorms, ClearOrFair, Clouds, ClearOrFair, RainOrShowers, ClearOrFair, RainOrShowers, RainOrShowers, ClearOrFair,
        ClearOrFair, ClearOrFair, Thunder, Thunderstorms, ClearOrFair, Fog, ClearOrFair, ClearOrFair, ClearOrFair, Clouds,
        ClearOrFair, ClearOrFair, DustStorms, ClearOrFair, ClearOrFair, ClearOrFair, Showers, Fog, ClearOrFair, HeatWaves,
        ClearOrFair, HeatWaves, ClearOrFair, ClearOrFair, ClearOrFair, Clouds, Fog, ClearOrFair, Fog, Blizzards,
        ClearOrFair, ClearOrFair, Blizzards | Snow, ClearOrFair, ClearOrFair, ClearOrFair, Gloom, ClearOrFair, ClearOrFair, ClearOrFair,
    ];

    private static readonly FrozenDictionary<ushort, Vector3> approachPoints = new Dictionary<ushort, Vector3>
    {
        [22] = new(211.50587f, 113.49627f, -216.74307f),
        [31] = new(-428.5311f, 69.81996f, 28.43267f),
        [32] = new(382.1901f, 5.188155f, 198.83205f),
        [36] = new(-301.0401f, 5.3800077f, -570.60455f),
        [38] = new(171.93164f, 17.499977f, -266.29816f),
        [39] = new(97.25184f, 2.5988786f, -73.968216f),
        [45] = new(-340.97287f, 21.293953f, 625.1788f),
        [50] = new(-286.03262f, -8.654527f, 272.75266f),
        [63] = new(34.256702f, 36.56745f, 213.31671f),
        [66] = new(-73.622925f, 73.56991f, -196.73103f),
        [69] = new(190.3045f, 234.47398f, 406.9958f),
        [70] = new(-483.05612f, 209.48744f, -279.20096f),
        [72] = new(-683.1892f, 315.5668f, 373.26578f),
        [104] = new(867.34906f, 47.032375f, -32.1302f),
        [117] = new(543.2363f, 219.76675f, 652.7301f),
        [133] = new(-392.68234f, 113.04094f, 122.56957f),
        [140] = new(-595.59674f, -169.00003f, -366.43912f),
        [153] = new(181.46855f, 165.69328f, -782.3936f),
        [162] = new(678.4738f, 70f, 512.5864f),
        [163] = new(-777.7409f, 240.90013f, 28.044926f),
        [172] = new(506.11508f, 58.843567f, 790.02814f),
        [179] = new(-325.8019f, 94.82681f, -755.7113f),
        [181] = new(-312.05276f, 59.25094f, 507.4568f),
        [201] = new(1.2207426f, 33.522175f, -474.66684f),
        [209] = new(-192.23924f, 35.58688f, -77.07811f),
        [210] = new(44.42447f, -2.6557508f, -128.63736f),
        [212] = new(-10.925671f, 48.05f, -2.1686547f),
        [227] = new(587.98975f, -42.814953f, -384.24127f),
        [239] = new(-390.17166f, 38.664627f, 548.02734f),
        [241] = new(-854.3621f, -82.97393f, 290.2913f),
        [251] = new(34.487827f, -16.146997f, 228.52016f),
        [254] = new(0.30054197f, 2.5105362f, -53.76743f),
        [270] = new(53.078583f, 117.62871f, -91.06518f),
    }.ToFrozenDictionary();

    private static readonly FrozenDictionary<ushort, VistaApproach> approaches = new Dictionary<ushort, VistaApproach>
    {
        [22] = VistaApproach.Indoors,
        [35] = VistaApproach.JumpPuzzle,
        [36] = VistaApproach.Indoors,
        [38] = VistaApproach.JumpPuzzle,
        [50] = VistaApproach.Ledge,
        [63] = VistaApproach.Ledge,
        [66] = VistaApproach.Ledge,
        [69] = VistaApproach.Indoors,
        [140] = VistaApproach.Indoors,
        [162] = VistaApproach.NpcGate,
        [165] = VistaApproach.JumpPuzzle,
        [168] = VistaApproach.JumpPuzzle,
        [212] = VistaApproach.JumpPuzzle,
        [263] = VistaApproach.JumpPuzzle,
        [265] = VistaApproach.Indoors,
        [300] = VistaApproach.JumpPuzzle,
    }.ToFrozenDictionary();

    public static uint WeatherMask(ushort number)
        => number >= 1 && number <= realmRebornWeathers.Length ? realmRebornWeathers[number - 1] : 0u;

    public static bool TryGetApproachPoint(ushort number, out Vector3 point) => approachPoints.TryGetValue(number, out point);

    public static VistaApproach Approach(ushort number) => approaches.TryGetValue(number, out var approach) ? approach : VistaApproach.Open;
}
