using System.Collections.Generic;
using UnityEngine;

public class TrackTerrainBuilder
{
    public Vector3 Center { get; private set; }
    public Vector3 Origin { get; private set; }
    public float Size { get; private set; }
    public int Resolution { get; private set; }
    public bool SnowCoveredGround => profile != null && profile.snowCoveredGround;
    public GameObject TerrainObject { get; private set; }
    private float[,] heights;
    private float roadHalfWidth;
    private float shoulderWidth;
    private IReadOnlyList<Vector3> samples;
    private float[] sampleDistances;
    private TrackEnvironmentProfile profile;
    private int seed;

    public void Build(Transform parent, TrackEnvironmentProfile environment, TrackBuilder builder, Material groundMaterial, Material snowMaterial, float halfRoadWidth, int randomSeed)
    {
        profile = environment;
        samples = builder.Samples;
        sampleDistances = new float[samples.Count];
        float pathLength = 0f;
        for (int i = 1; i < samples.Count; i++)
        {
            pathLength += Vector3.Distance(samples[i - 1], samples[i]);
            sampleDistances[i] = pathLength;
        }
        pathLength += Vector3.Distance(samples[samples.Count - 1], samples[0]);
        seed = randomSeed;
        roadHalfWidth = halfRoadWidth;
        shoulderWidth = environment.shoulderWidth;
        Resolution = Mathf.Clamp(environment.terrainResolution, 65, 257);
        Size = environment.terrainSize;

        Vector3 min = samples[0];
        Vector3 max = samples[0];
        for (int i = 1; i < samples.Count; i++)
        {
            min = Vector3.Min(min, samples[i]);
            max = Vector3.Max(max, samples[i]);
        }
        Center = new Vector3((min.x + max.x) * 0.5f, 0f, (min.z + max.z) * 0.5f);
        Origin = new Vector3(Center.x - Size * 0.5f, -160f, Center.z - Size * 0.5f);
        heights = new float[Resolution, Resolution];
        List<Vector3> vertices = new List<Vector3>(Resolution * Resolution);
        List<Vector2> uvs = new List<Vector2>(Resolution * Resolution);
        List<int> groundTriangles = new List<int>((Resolution - 1) * (Resolution - 1) * 6);
        List<int> snowTriangles = new List<int>(groundTriangles.Capacity);
        float step = Size / (Resolution - 1);
        float corridor = roadHalfWidth + shoulderWidth + 0.05f;

        for (int z = 0; z < Resolution; z++)
        {
            for (int x = 0; x < Resolution; x++)
            {
                float worldX = Origin.x + x * step;
                float worldZ = Origin.z + z * step;
                Vector2 position = new Vector2(worldX, worldZ);
                float nearestDistance;
                float roadHeight;
                float lateral;
                float along;
                NearestRoadInfo(position, out nearestDistance, out roadHeight, out lateral, out along);
                float natural = NaturalHeight(worldX, worldZ);
                float startDistance = Mathf.Min(along, builder.TrackLength - along);
                if (startDistance <= TrackBuilder.StartSafeZoneMeters) natural = roadHeight - 0.16f;
                float blend = SmoothStep(0f, 1f, (nearestDistance - corridor) / Mathf.Max(1f, profile.roadCutBlend));
                float height = Mathf.Lerp(roadHeight - 0.16f, natural, blend);
                if (profile.enableRoadsideDitches && !profile.seaEnabled && nearestDistance > corridor && startDistance > TrackBuilder.StartSafeZoneMeters + 4f)
                {
                    float ditchCenter = corridor + 2.1f;
                    float ditch = Mathf.Exp(-Mathf.Pow((nearestDistance - ditchCenter) / 0.85f, 2f));
                    if (lateral < 0f) height -= profile.roadsideDitchDepth * ditch;
                }
                if (profile.retainingWallDrop > 0f && nearestDistance > corridor && startDistance > TrackBuilder.StartSafeZoneMeters + 4f)
                {
                    float wallCut = SmoothStep(corridor, corridor + 1.1f, nearestDistance);
                    height = Mathf.Min(height, roadHeight - 0.16f - profile.retainingWallDrop * wallCut);
                }
                if (profile.seaEnabled && worldZ < profile.coastlineZ && nearestDistance > corridor + profile.roadCutBlend)
                {
                    float coast = SmoothStep(0f, 1f, (profile.coastlineZ - worldZ) / Mathf.Max(1f, profile.coastBlend));
                    height = Mathf.Lerp(height, profile.seaLevel - 3f, coast);
                }
                if (profile.seaEnabled && nearestDistance > corridor + 1f)
                {
                    float shore = 1f - SmoothStep(0f, 7f, Mathf.Abs(worldZ - profile.coastlineZ));
                    float beachHeight = profile.seaLevel - 0.12f + (ValueNoise(worldX, worldZ, seed + 660, 0.04f) - 0.5f) * 0.35f;
                    height = Mathf.Lerp(height, beachHeight, shore);
                }
                height = ApplyBridgeCarvings(builder, along, nearestDistance, roadHeight, height, corridor);
                float edgeDistance = Mathf.Max(Mathf.Abs(worldX - Center.x), Mathf.Abs(worldZ - Center.z));
                float edgeFade = SmoothStep(Size * 0.36f, Size * 0.5f, edgeDistance);
                height = Mathf.Lerp(height, profile.seaEnabled ? profile.seaLevel - 3f : -6f, edgeFade);
                heights[z, x] = height;
                vertices.Add(new Vector3(worldX, height, worldZ));
                uvs.Add(new Vector2(worldX, worldZ));
            }
        }

        for (int z = 0; z < Resolution - 1; z++)
        {
            for (int x = 0; x < Resolution - 1; x++)
            {
                int a = z * Resolution + x;
                int b = a + 1;
                int c = a + Resolution;
                int d = c + 1;
                float averageHeight = (heights[z, x] + heights[z, x + 1] + heights[z + 1, x] + heights[z + 1, x + 1]) * 0.25f;
                float snowNoise = ValueNoise(Origin.x + x * step, Origin.z + z * step, seed + 810, 0.035f);
                float snowWeight = profile.snowCoveredGround ? 1f : SmoothStep(profile.snowStartHeight - profile.snowBlendHeight, profile.snowStartHeight + profile.snowBlendHeight, averageHeight + snowNoise * 2f);
                List<int> target = snowWeight > 0.52f && snowMaterial != null ? snowTriangles : groundTriangles;
                target.AddRange(new[] { a, c, b, b, c, d });
            }
        }

        Mesh mesh = TrackMeshBuilder.Create("Terrain", vertices, uvs, new[] { groundTriangles, snowTriangles }, new[] { groundMaterial, snowMaterial });
        TerrainObject = TrackMeshBuilder.CreateObject("Terrain", mesh, new[] { groundMaterial, snowMaterial }, parent, true, false);
        TerrainObject.AddComponent<TrackSurface1912>().Configure(this);
        TerrainObject.GetComponent<MeshCollider>().convex = false;
    }

    public float SampleHeight(Vector3 worldPosition)
    {
        if (heights == null) return -0.16f;
        float fx = Mathf.Clamp((worldPosition.x - Origin.x) / Size * (Resolution - 1), 0f, Resolution - 1);
        float fz = Mathf.Clamp((worldPosition.z - Origin.z) / Size * (Resolution - 1), 0f, Resolution - 1);
        int x = Mathf.Min(Mathf.FloorToInt(fx), Resolution - 2);
        int z = Mathf.Min(Mathf.FloorToInt(fz), Resolution - 2);
        float tx = fx - x;
        float tz = fz - z;
        float a = heights[z, x];
        float b = heights[z, x + 1];
        float c = heights[z + 1, x];
        float d = heights[z + 1, x + 1];
        if (tx + tz <= 1f) return a + (b - a) * tx + (c - a) * tz;
        return b * (1f - tz) + c * (1f - tx) + d * (tx + tz - 1f);
    }

    public bool IsSnowAt(Vector3 worldPosition)
    {
        if (profile.snowCoveredGround || heights == null) return profile.snowCoveredGround;
        float fx = Mathf.Clamp((worldPosition.x - Origin.x) / Size * (Resolution - 1), 0f, Resolution - 1);
        float fz = Mathf.Clamp((worldPosition.z - Origin.z) / Size * (Resolution - 1), 0f, Resolution - 1);
        int x = Mathf.Min(Mathf.FloorToInt(fx), Resolution - 2);
        int z = Mathf.Min(Mathf.FloorToInt(fz), Resolution - 2);
        float averageHeight = (heights[z, x] + heights[z, x + 1] + heights[z + 1, x] + heights[z + 1, x + 1]) * 0.25f;
        float step = Size / (Resolution - 1);
        float snowNoise = ValueNoise(Origin.x + x * step, Origin.z + z * step, seed + 810, 0.035f);
        float snowWeight = SmoothStep(profile.snowStartHeight - profile.snowBlendHeight, profile.snowStartHeight + profile.snowBlendHeight, averageHeight + snowNoise * 2f);
        return snowWeight > 0.52f;
    }

    public float DistanceToRoad(Vector3 worldPosition, out float roadHeight)
    {
        float lateral;
        float along;
        NearestRoadInfo(new Vector2(worldPosition.x, worldPosition.z), out float distance, out roadHeight, out lateral, out along);
        return distance;
    }

    private void NearestRoadInfo(Vector2 position, out float distance, out float roadHeight, out float lateral, out float along)
    {
        float nearest = float.MaxValue;
        roadHeight = 0f;
        lateral = 0f;
        along = 0f;
        for (int i = 0; i < samples.Count; i++)
        {
            int next = (i + 1) % samples.Count;
            Vector3 a = samples[i];
            Vector3 b = samples[next];
            float abx = b.x - a.x;
            float abz = b.z - a.z;
            float lengthSquared = abx * abx + abz * abz;
            float t = lengthSquared > 0.0001f ? Mathf.Clamp01(((position.x - a.x) * abx + (position.y - a.z) * abz) / lengthSquared) : 0f;
            Vector3 closest = Vector3.Lerp(a, b, t);
            float dx = position.x - closest.x;
            float dz = position.y - closest.z;
            float candidate = dx * dx + dz * dz;
            if (candidate >= nearest) continue;
            nearest = candidate;
            roadHeight = closest.y;
            float segmentLength = Vector3.Distance(a, b);
            along = sampleDistances[i] + segmentLength * t;
            Vector3 tangent = (b - a).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
            lateral = dx * right.x + dz * right.z;
        }
        distance = Mathf.Sqrt(nearest);
    }

    private static float ApplyBridgeCarvings(TrackBuilder builder, float along, float roadDistance, float roadHeight, float height, float corridor)
    {
        if (builder == null || builder.track == null) return height;
        foreach (TrackData.TrackSection section in builder.EffectiveSections)
        {
            if (section == null || section.kind != TrackData.TrackSectionKind.StoneBridge) continue;
            float length = Mathf.Clamp(section.lengthMeters, 24f, Mathf.Min(72f, builder.TrackLength * 0.2f));
            float start = Mathf.Repeat(section.startProgress, 1f) * builder.TrackLength;
            float wrapped = Mathf.Repeat(along - start, Mathf.Max(1f, builder.TrackLength));
            if (wrapped > length) continue;
            float t = Mathf.Clamp01(wrapped / length);
            float envelope = Mathf.Sin(t * Mathf.PI);
            float blend = 1f - SmoothStep(corridor + 1.2f, corridor + 5f, roadDistance);
            height = Mathf.Min(height, roadHeight - 0.18f - envelope * 4.2f * blend);
        }
        return height;
    }

    private float NaturalHeight(float x, float z)
    {
        float broad = ValueNoise(x, z, seed, profile.terrainNoiseScale) * 2f - 1f;
        float detail = ValueNoise(x, z, seed + 170, profile.terrainNoiseScale * 3.7f) * 2f - 1f;
        float baseHeight = -0.24f + broad * profile.terrainRelief + detail * profile.terrainRelief * 0.18f;
        if (profile.mountainHeight > 0f)
        {
            float distance = Vector2.Distance(new Vector2(x, z), new Vector2(Center.x, Center.z));
            float mountainBlend = SmoothStep(65f, Mathf.Max(120f, Size * 0.46f), distance);
            float ridge = 1f - Mathf.Abs(ValueNoise(x, z, seed + 420, 0.006f) * 2f - 1f);
            baseHeight += mountainBlend * profile.mountainHeight * (0.28f + ridge * 0.72f);
        }
        return baseHeight;
    }

    private static float ValueNoise(float x, float z, int noiseSeed, float scale)
    {
        x *= scale;
        z *= scale;
        int ix = Mathf.FloorToInt(x);
        int iz = Mathf.FloorToInt(z);
        float tx = SmoothStep(0f, 1f, x - ix);
        float tz = SmoothStep(0f, 1f, z - iz);
        float a = Hash(ix, iz, noiseSeed);
        float b = Hash(ix + 1, iz, noiseSeed);
        float c = Hash(ix, iz + 1, noiseSeed);
        float d = Hash(ix + 1, iz + 1, noiseSeed);
        return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz);
    }

    private static float Hash(int x, int z, int noiseSeed)
    {
        unchecked
        {
            uint hash = (uint)noiseSeed * 374761393u + (uint)x * 668265263u + (uint)z * 2147483647u;
            hash = (hash ^ (hash >> 13)) * 1274126177u;
            return (hash & 0x00ffffffu) / 16777215f;
        }
    }

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        float t = Mathf.Clamp01((value - edge0) / Mathf.Max(0.0001f, edge1 - edge0));
        return t * t * (3f - 2f * t);
    }
}
