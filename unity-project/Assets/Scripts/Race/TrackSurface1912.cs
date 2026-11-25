using UnityEngine;

public class TrackSurface1912 : MonoBehaviour
{
    private TrackTerrainBuilder terrain;

    public void Configure(TrackTerrainBuilder source)
    {
        terrain = source;
    }

    public bool IsSnowAt(Vector3 worldPosition)
    {
        return terrain != null && terrain.IsSnowAt(worldPosition);
    }
}
