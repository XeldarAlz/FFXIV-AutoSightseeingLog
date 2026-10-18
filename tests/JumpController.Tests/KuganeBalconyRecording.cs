using AutoSightseeingLog.Core.Tasks;
using System.Globalization;
using System.Numerics;

internal static class KuganeBalconyRecording
{
    public static JumpSample[] Read(string file = "kugane-balcony-20261018.csv")
        => File.ReadLines(Path.Combine(AppContext.BaseDirectory, "Fixtures", file))
            .Skip(1).Select(line =>
            {
                var fields = line.Split(',');
                return new JumpSample(new Vector3(Number(fields[1]), Number(fields[2]), Number(fields[3])),
                    bool.Parse(fields[5]), double.Parse(fields[0], CultureInfo.InvariantCulture));
            }).ToArray();

    public static List<JumpSample[]> Flights(JumpSample[] frames)
    {
        var flights = new List<JumpSample[]>();
        var start = -1;
        for (var index = 1; index < frames.Length; index++)
        {
            if (frames[index].Airborne && start < 0)
                start = index - 1;
            else if (!frames[index].Airborne && start >= 0)
            {
                flights.Add(frames[start..(index + 1)]);
                start = -1;
            }
        }

        return flights;
    }

    private static float Number(string value) => float.Parse(value, CultureInfo.InvariantCulture);
}
