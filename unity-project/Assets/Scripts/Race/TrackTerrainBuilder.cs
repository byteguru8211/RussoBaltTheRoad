using System.Collections.Generic;
using UnityEngine;

public class TrackTerrainBuilder
{
    public Vector3 Center { get; private set; }
    public Vector3 Origin { get; private set; }
    public float Size { get; private set; }
    public int Resolution { get; private set; }
    public GameObject TerrainObject { get; private set; }
    private float[,] heights;
    private float roadHalfWidth;
    private float shoulderWidth;
    private IReadOnlyList<Vector3> samples;
    private TrackEnvironmentProfile profile;
    private int seed;

    public void Build(Transform parent, TrackEnvironmentProfile environment, IReadOnlyList<Vector3> path, Material groundMaterial, Material snowMaterial, float halfRoadWidth, int randomSeed)
    {
        profile = environment;
        samples = path;
        seed = randomSeed;
        roadHalfWidth = halfRoadWidth;
        shoulderWidth = environment.shoulderWidth;
        Resolution = Mathf.Clamp(environment.terrainResolution, 65, 257);
        Size = environment.terrainSize;

        Vector3 min = path[0];
        Vector3 max = path[0];
        for (int i = 1; i < path.Count; i++)
        {
            min = Vector3.Min(min, path[i]);
            max = Vector3.Max(max, path[i]);
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
                float roadHeight = NearestRoadHeight(position, out nearestDistance);
                float natural = NaturalHeight(worldX, worldZ);
                float blend = SmoothStep(0f, 1f, (nearestDistance - corridor) / Mathf.Max(1f, profile.roadCutBlend));
                float height = Mathf.Lerp(roadHeight - 0.16f, natural, blend);
                if (profile.retainingWallDrop > 0f && nearestDistance > corridor)
                {
                    float wallCut = SmoothStep(corridor, corridor + 1.1f, nearestDistance);
                    height = Mathf.Min(height, roadHeight - 0.16f - profile.retainingWallDrop * wallCut);
                }
                if (profile.seaEnabled && worldZ < profile.coastlineZ && nearestDistance > corridor + profile.roadCutBlend)
                {
                    float coast = SmoothStep(0f, 1f, (profile.coastlineZ - worldZ) / Mathf.Max(1f, profile.coastBlend));
                    height = Mathf.Lerp(height, profile.seaLevel - 3f, coast);
                }
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
        float fx = Mathf.Clamp((worldPosition.x - Origin.x) / Size * (Resolution - 1), 0f, Resolution - 1.001f);
        float fz = Mathf.Clamp((worldPosition.z - Origin.z) / Size * (Resolution - 1), 0f, Resolution - 1.001f);
        int x = Mathf.FloorToInt(fx);
        int z = Mathf.FloorToInt(fz);
        float tx = fx - x;
        float tz = fz - z;
        float a = Mathf.Lerp(heights[z, x], heights[z, x + 1], tx);
        float b = Mathf.Lerp(heights[z + 1, x], heights[z + 1, x + 1], tx);
        return Mathf.Lerp(a, b, tz);
    }

    public bool IsSnowAt(Vector3 worldPosition)
    {
        if (profile.snowCoveredGround) return true;
        float detail = ValueNoise(worldPosition.x, worldPosition.z, seed + 810, 0.035f) * 2f - 1f;
        float t = SmoothStep(profile.snowStartHeight - profile.snowBlendHeight, profile.snowStartHeight + profile.snowBlendHeight, worldPosition.y + detail * 2f);
        return t > 0.5f;
    }

    public float DistanceToRoad(Vector3 worldPosition, out float roadHeight)
    {
        return NearestRoadHeight(new Vector2(worldPosition.x, worldPosition.z), out roadHeight);
    }

    private float NearestRoadHeight(Vector2 position, out float distance)
    {
        float nearest = float.MaxValue;
        float height = 0f;
        for (int i = 0; i < samples.Count; i++)
        {
            Vector3 a = samples[i];
            Vector3 b = samples[(i + 1) % samples.Count];
            float abx = b.x - a.x;
            float abz = b.z - a.z;
            float lengthSquared = abx * abx + abz * abz;
            float t = lengthSquared > 0.0001f ? Mathf.Clamp01(((position.x - a.x) * abx + (position.y - a.z) * abz) / lengthSquared) : 0f;
            float dx = position.x - (a.x + abx * t);
            float dz = position.y - (a.z + abz * t);
            float candidate = dx * dx + dz * dz;
            if (candidate < nearest)
            {
                nearest = candidate;
                height = Mathf.Lerp(a.y, b.y, t);
            }
        }
        distance = Mathf.Sqrt(nearest);
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
