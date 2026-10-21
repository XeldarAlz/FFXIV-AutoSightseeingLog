using System.Numerics;

namespace AutoSightseeingLog.Core.Travel;

internal static class MapMarkerPosition
{
    // Marker pixels span a 2048-pixel texture centred on the map's origin.
    private const float CentrePixel = 1024f;

    // A marker has no height, so the result sits at 0.
    public static Vector3 ToWorld(float x, float y, float sizeFactor, float offsetX, float offsetY)
    {
        var scale = sizeFactor * 0.01f;
        return new Vector3((x - CentrePixel) / scale - offsetX, 0f, (y - CentrePixel) / scale - offsetY);
    }
}
