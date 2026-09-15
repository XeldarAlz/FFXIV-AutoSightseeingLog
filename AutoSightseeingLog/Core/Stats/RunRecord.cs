using Newtonsoft.Json;

namespace AutoSightseeingLog.Core.Stats;

[Serializable]
public sealed class RunRecord
{
    public DateTime StartedAtUtc { get; set; }
    public DateTime EndedAtUtc { get; set; }
    public double DurationSeconds { get; set; }

    public int VistasLogged { get; set; }
    public int ZonesVisited { get; set; }

    public List<string> VistaNames { get; set; } = [];

    [JsonIgnore]
    public TimeSpan Duration => TimeSpan.FromSeconds(DurationSeconds);

    [JsonIgnore]
    public double VistasPerHour => DurationSeconds > 0 ? VistasLogged / (DurationSeconds / 3600.0) : 0;
}
