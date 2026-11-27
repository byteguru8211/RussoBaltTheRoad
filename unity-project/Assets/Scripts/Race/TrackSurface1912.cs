using UnityEngine;

public class TrackSurface1912 : MonoBehaviour
{
    public enum SurfaceKind { Dirt, Cobblestone, Snow, Ice, Grass, Stone }

    public SurfaceKind Kind { get; private set; } = SurfaceKind.Dirt;
    public float Grip { get; private set; } = 1f;
    public float RollingResistanceMultiplier { get; private set; } = 1f;
    public float Wetness { get; private set; }
    public float EffectiveGrip => Grip * (1f - Wetness * 0.16f);

    private TrackTerrainBuilder terrain;

    public void Configure(TrackTerrainBuilder source)
    {
        terrain = source;
        Kind = source != null && source.SnowCoveredGround ? SurfaceKind.Snow : SurfaceKind.Grass;
        Grip = Kind == SurfaceKind.Snow ? 0.72f : 0.82f;
        RollingResistanceMultiplier = Kind == SurfaceKind.Snow ? 1.35f : 1.1f;
    }

    public void Configure(SurfaceKind kind, float grip = 1f, float rollingResistance = 1f)
    {
        Kind = kind;
        Grip = Mathf.Clamp(grip, 0.1f, 1.5f);
        RollingResistanceMultiplier = Mathf.Clamp(rollingResistance, 0.5f, 2.5f);
    }

    public void SetWetness(float value)
    {
        Wetness = Mathf.Clamp01(value);
    }

    public bool IsWetAt(Vector3 worldPosition) => Wetness > 0.01f;

    public bool IsSnowAt(Vector3 worldPosition)
    {
        return Kind == SurfaceKind.Snow || terrain != null && terrain.IsSnowAt(worldPosition);
    }

    public SurfaceKind GetSurfaceKindAt(Vector3 worldPosition)
    {
        return terrain != null && terrain.IsSnowAt(worldPosition) ? SurfaceKind.Snow : Kind;
    }
}
