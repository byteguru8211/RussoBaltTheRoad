using System.Collections.Generic;
using UnityEngine;

public static class TrackSceneryBuilder
{
    private sealed class CullableTree
    {
        public GameObject root;
        public LODGroup lodGroup;
        public MeshRenderer[] renderers;
    }

    private static readonly List<CullableTree> cullableTrees = new List<CullableTree>();

    public static void Build(Transform parent, TrackEnvironmentProfile profile, TrackBuilder builder, TrackTerrainBuilder terrain, System.Random random, Material snowMaterial, Material rockMaterial, Material stoneMaterial, Material warmStoneMaterial, Material roofMaterial, Material glassMaterial, Material woodMaterial, Material foliageMaterial, Material sailMaterial, Material metalMaterial, Material lampMaterial, Material seaMaterial, Material groundMaterial)
    {
        IReadOnlyList<Vector3> samples = builder.Samples;
        string scenery = string.IsNullOrWhiteSpace(builder.track.scenery) ? "forest" : builder.track.scenery.ToLowerInvariant();
        CreateTrees(parent, profile, builder, terrain, random, scenery, snowMaterial, woodMaterial, foliageMaterial);
        CreateRocks(parent, profile, builder, terrain, random, rockMaterial);
        CreateBuildings(parent, profile, builder, terrain, random, scenery, stoneMaterial, warmStoneMaterial, roofMaterial, glassMaterial);
        CreateHorizon(parent, profile, terrain.Center, random);
        if (profile.seaEnabled) CreateSea(parent, profile, terrain.Center, seaMaterial);
        else CreateDistantGround(parent, terrain.Center, groundMaterial);
        if (profile.harborEnabled) CreateHarbor(parent, profile, samples, terrain, random, stoneMaterial, woodMaterial, sailMaterial, metalMaterial, lampMaterial);
    }

    private static void CreateTrees(Transform parent, TrackEnvironmentProfile profile, TrackBuilder builder, TrackTerrainBuilder terrain, System.Random random, string scenery, Material snowMaterial, Material woodMaterial, Material foliageMaterial)
    {
        IReadOnlyList<Vector3> samples = builder.Samples;
        float halfRoadWidth = builder.track.roadWidth * 0.5f;
        float density = scenery switch
        {
            "town" => 0.55f,
            "speedway" => 0.7f,
            "coast" => 0.8f,
            _ => 1f
        };
        int treeCount = Mathf.Max(12, Mathf.RoundToInt(profile.treeCount * density));
        int clusterCount = profile.treeClusterCount > 0 ? Mathf.Clamp(profile.treeClusterCount, 3, 12) : 7;
        float clusterRadius = profile.treeClusterRadius > 0f ? profile.treeClusterRadius : 34f;
        List<Vector3> clusters = new List<Vector3>();
        List<Vector3> occupied = new List<Vector3>();
        float minimumZ = profile.seaEnabled ? profile.coastlineZ + 5f : float.NegativeInfinity;
        for (int i = 0; i < clusterCount; i++)
        {
            if (TryFindSceneryPosition(samples, terrain, random, builder, halfRoadWidth + profile.shoulderWidth + 9f,
                Mathf.Max(12f, profile.sceneryOffsetMin), Mathf.Max(18f, profile.sceneryOffsetMax), occupied, 16f,
                minimumZ, false, Vector3.zero, clusterRadius, out Vector3 cluster))
            {
                clusters.Add(cluster);
                occupied.Add(cluster);
            }
        }
        for (int i = 0; i < treeCount; i++)
        {
            bool clustered = clusters.Count > 0 && random.NextDouble() < 0.86f;
            Vector3 cluster = clustered ? clusters[random.Next(clusters.Count)] : Vector3.zero;
            if (!TryFindSceneryPosition(samples, terrain, random, builder, halfRoadWidth + profile.shoulderWidth + profile.barrierOffset + 1.2f,
                profile.sceneryOffsetMin, profile.sceneryOffsetMax, occupied, 4.2f, minimumZ, clustered, cluster, clusterRadius, out Vector3 position)) continue;
            position.y = terrain.SampleHeight(position);
            string resource = SelectTreeResource(profile, scenery, random);
            GameObject prefab = Resources.Load<GameObject>($"Environment/ReleaseKit/{ReleaseTreeResource(resource)}");
            if (prefab == null) prefab = Resources.Load<GameObject>($"Environment/{resource}");
            GameObject tree;
            if (prefab != null)
            {
                tree = InstantiateAsset(prefab, position, Quaternion.Euler(0f, random.Next(0, 360), 0f), parent);
                float scale = TrackMeshBuilder.RandomRange(random, profile.treeScaleMin, profile.treeScaleMax);
                tree.transform.localScale = prefab.transform.localScale * scale;
                GroundInstance(tree, position.y);
            }
            else
            {
                tree = CreateFallbackTree(parent, position, random, resource == "Birch", woodMaterial, foliageMaterial);
            }
            tree.name = "Tree_" + resource + "_" + i.ToString("000");
            SetStatic(tree);
            if (!HasRoadClearance(tree, builder, true))
            {
                if (Application.isPlaying) Object.Destroy(tree);
                else Object.DestroyImmediate(tree);
                continue;
            }
            occupied.Add(position);
            if (profile.treeSnowChance > 0f && random.NextDouble() < profile.treeSnowChance)
                AddSnowCap(tree, random, snowMaterial, resource != "Birch");
            RegisterTreeCulling(tree);
        }
    }

    private static string SelectTreeResource(TrackEnvironmentProfile profile, string scenery, System.Random random)
    {
        if (scenery == "coast" || scenery == "town" || scenery == "speedway")
        {
            string[] biomeTrees = scenery == "coast" ? new[] { "Oak", "Birch", "Oak", "Pine" } : new[] { "Oak", "Birch" };
            return biomeTrees[random.Next(biomeTrees.Length)];
        }
        return profile.treeResources != null && profile.treeResources.Length > 0 ? profile.treeResources[random.Next(profile.treeResources.Length)] : "Pine";
    }

    private static string ReleaseTreeResource(string resource)
    {
        return resource.ToLowerInvariant() switch
        {
            "pine" or "conifer" => "Tree_Conifer_Detailed_01",
            "birch" => "Tree_Birch_Detailed_01",
            "oak" or "broadleaf" => "Tree_Oak_Detailed_01",
            _ => resource
        };
    }

    private static GameObject CreateFallbackTree(Transform parent, Vector3 position, System.Random random, bool birch, Material woodMaterial, Material foliageMaterial)
    {
        List<Vector3> trunkVertices = new List<Vector3>();
        List<Vector2> trunkUvs = new List<Vector2>();
        List<int> trunkTriangles = new List<int>();
        List<Vector3> crownVertices = new List<Vector3>();
        List<Vector2> crownUvs = new List<Vector2>();
        List<int> crownTriangles = new List<int>();
        float height = TrackMeshBuilder.RandomRange(random, 4.5f, 7.5f);
        TrackMeshBuilder.AddCylinder(trunkVertices, trunkUvs, trunkTriangles, position + Vector3.up * height * 0.35f, 0.2f, 0.12f, height * 0.7f, 7, Quaternion.identity);
        if (birch)
        {
            TrackMeshBuilder.AddRock(crownVertices, crownUvs, crownTriangles, position + Vector3.up * height * 0.78f, new Vector3(1.7f, 2.1f, 1.6f), random.Next());
        }
        else
        {
            for (int i = 0; i < 3; i++)
            {
                float coneHeight = height * (0.48f - i * 0.07f);
                float radius = height * (0.25f - i * 0.045f);
                TrackMeshBuilder.AddCylinder(crownVertices, crownUvs, crownTriangles, position + Vector3.up * height * (0.48f + i * 0.17f), radius, 0f, coneHeight, 9, Quaternion.identity);
            }
        }
        GameObject tree = new GameObject("Tree_Fallback");
        tree.transform.SetParent(parent, false);
        Mesh trunk = TrackMeshBuilder.Create("TreeTrunk", trunkVertices, trunkUvs, new[] { trunkTriangles }, new[] { woodMaterial });
        Mesh crown = TrackMeshBuilder.Create("TreeCrown", crownVertices, crownUvs, new[] { crownTriangles }, new[] { foliageMaterial });
        TrackMeshBuilder.CreateObject("Trunk", trunk, new[] { woodMaterial }, tree.transform, false, true);
        TrackMeshBuilder.CreateObject("Crown", crown, new[] { foliageMaterial }, tree.transform, false, true);
        return tree;
    }

    private static void AddSnowCap(GameObject tree, System.Random random, Material snowMaterial, bool conifer)
    {
        Bounds bounds = GetBounds(tree);
        if (float.IsNaN(bounds.extents.sqrMagnitude) || bounds.size.y < 1f) return;
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();
        float height = bounds.size.y * (conifer ? 0.3f : 0.16f);
        float radius = bounds.size.x * (conifer ? 0.23f : 0.3f);
        Vector3 worldCenter = new Vector3(bounds.center.x, bounds.max.y - height * 0.25f, bounds.center.z);
        Quaternion worldRotation = Quaternion.Euler(0f, random.Next(0, 360), 0f);
        Vector3 lossyScale = tree.transform.lossyScale;
        Vector3 localCenter = tree.transform.InverseTransformPoint(worldCenter);
        Quaternion localRotation = Quaternion.Inverse(tree.transform.rotation) * worldRotation;
        float localHeight = height / Mathf.Max(0.001f, lossyScale.y);
        float localRadius = radius / Mathf.Max(0.001f, Mathf.Max(lossyScale.x, lossyScale.z));
        TrackMeshBuilder.AddCylinder(vertices, uvs, triangles, localCenter, localRadius, localRadius * (conifer ? 0.08f : 0.86f), localHeight, 9, localRotation);
        Mesh mesh = TrackMeshBuilder.Create("SnowCap", vertices, uvs, new[] { triangles }, new[] { snowMaterial });
        TrackMeshBuilder.CreateObject("SnowCap", mesh, new[] { snowMaterial }, tree.transform, false, false);
    }

    private static bool TryFindSceneryPosition(IReadOnlyList<Vector3> samples, TrackTerrainBuilder terrain, System.Random random, TrackBuilder builder, float minimumRoadDistance, float minimumOffset, float maximumOffset, List<Vector3> occupied, float occupiedSpacing, float minimumZ, bool clustered, Vector3 clusterCenter, float clusterRadius, out Vector3 result)
    {
        result = Vector3.zero;
        for (int attempt = 0; attempt < 32; attempt++)
        {
            int sample = clustered ? NearestSampleIndex(samples, clusterCenter) : random.Next(samples.Count);
            Vector3 point = samples[sample];
            Vector3 tangent = samples[(sample + 1) % samples.Count] - point;
            Vector3 right = Vector3.Cross(Vector3.up, tangent.normalized).normalized;
            int side = random.Next(2) == 0 ? -1 : 1;
            Vector3 candidate = point + right * side * TrackMeshBuilder.RandomRange(random, minimumOffset, maximumOffset);
            if (clustered && (candidate - clusterCenter).sqrMagnitude > clusterRadius * clusterRadius) continue;
            if (builder.RoadDistanceAt(candidate) < minimumRoadDistance || candidate.z < minimumZ || builder.IsStartSafeZone(candidate)) continue;
            bool clear = true;
            foreach (Vector3 other in occupied)
            {
                if ((other - candidate).sqrMagnitude < occupiedSpacing * occupiedSpacing)
                {
                    clear = false;
                    break;
                }
            }
            if (!clear) continue;
            result = candidate;
            return true;
        }
        return false;
    }

    private static int NearestSampleIndex(IReadOnlyList<Vector3> samples, Vector3 position)
    {
        int nearest = 0;
        float best = float.PositiveInfinity;
        for (int i = 0; i < samples.Count; i++)
        {
            float candidate = (samples[i] - position).sqrMagnitude;
            if (candidate >= best) continue;
            best = candidate;
            nearest = i;
        }
        return nearest;
    }

    private static void CreateRocks(Transform parent, TrackEnvironmentProfile profile, TrackBuilder builder, TrackTerrainBuilder terrain, System.Random random, Material rockMaterial)
    {
        if (profile.rockCount <= 0) return;
        IReadOnlyList<Vector3> samples = builder.Samples;
        float halfRoadWidth = builder.track.roadWidth * 0.5f;
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();
        List<Vector3> occupied = new List<Vector3>();
        for (int i = 0; i < profile.rockCount; i++)
        {
            Vector3 scale = new Vector3(TrackMeshBuilder.RandomRange(random, 1.2f, 4.8f), TrackMeshBuilder.RandomRange(random, 1.5f, 7.5f), TrackMeshBuilder.RandomRange(random, 1.2f, 4.2f));
            float rockRadius = Mathf.Max(scale.x, scale.z) * 1.2f + 0.65f;
            Vector3 position;
            if (!TryFindSceneryPosition(samples, terrain, random, builder, halfRoadWidth + profile.shoulderWidth + profile.barrierOffset + rockRadius, 9f, 44f, occupied, 3.5f, float.NegativeInfinity, false, Vector3.zero, 0f, out position)) continue;
            occupied.Add(position);
            position.y = terrain.SampleHeight(position);
            TrackMeshBuilder.AddRock(vertices, uvs, triangles, position + Vector3.up * scale.y * 0.28f, scale, profile.seed + i * 31);
        }
        Mesh mesh = TrackMeshBuilder.Create("AlpineRocks", vertices, uvs, new[] { triangles }, new[] { rockMaterial });
        TrackMeshBuilder.CreateObject("Rocks", mesh, new[] { rockMaterial }, parent, true, true);
    }

    private static void CreateBuildings(Transform parent, TrackEnvironmentProfile profile, TrackBuilder builder, TrackTerrainBuilder terrain, System.Random random, string scenery, Material stoneMaterial, Material warmStoneMaterial, Material roofMaterial, Material glassMaterial)
    {
        IReadOnlyList<Vector3> samples = builder.Samples;
        float halfRoadWidth = builder.track.roadWidth * 0.5f;
        bool coastal = profile.seaEnabled || scenery == "coast" || scenery == "town";
        GameObject prefab = Resources.Load<GameObject>(coastal
            ? "Environment/ReleaseKit/Building_Townhouse_Detailed_01"
            : "Environment/ReleaseKit/Building_Log_Cabin_Detailed_01");
        if (prefab == null) prefab = Resources.Load<GameObject>($"Environment/{profile.buildingResource}");
        List<Vector3> occupied = new List<Vector3>();
        int buildingCount = Mathf.Max(2, Mathf.RoundToInt(profile.buildingCount * (scenery == "speedway" ? 0.7f : 1f)));
        for (int i = 0; i < buildingCount; i++)
        {
            Vector3 position;
            if (!TryFindSceneryPosition(samples, terrain, random, builder, halfRoadWidth + profile.shoulderWidth + 6f, 14f, 32f, occupied, 8f, profile.seaEnabled ? profile.coastlineZ + 8f : float.NegativeInfinity, false, Vector3.zero, 0f, out position)) continue;
            occupied.Add(position);
            position.y = terrain.SampleHeight(position);
            int nearestIndex = NearestSampleIndex(samples, position);
            Vector3 point = samples[nearestIndex];
            Vector3 tangent = samples[(nearestIndex + 1) % samples.Count] - point;
            Vector3 right = Vector3.Cross(Vector3.up, tangent.normalized).normalized;
            int side = Vector3.Dot(position - point, right) < 0f ? -1 : 1;
            Quaternion rotation = Quaternion.LookRotation(-right * side, Vector3.up);
            GameObject building;
            if (prefab != null)
            {
                building = InstantiateAsset(prefab, position, rotation, parent);
                float width = TrackMeshBuilder.RandomRange(random, 0.85f, 1.25f);
                float height = coastal ? TrackMeshBuilder.RandomRange(random, 1.25f, 2.15f) : TrackMeshBuilder.RandomRange(random, 0.9f, 1.2f);
                Vector3 prefabScale = prefab.transform.localScale;
                building.transform.localScale = new Vector3(prefabScale.x * width, prefabScale.y * width, prefabScale.z * height);
                GroundInstance(building, position.y);
                if (coastal) ApplyStoneBuildingMaterials(building, stoneMaterial, warmStoneMaterial, roofMaterial, glassMaterial, random);
            }
            else
            {
                building = CreateFallbackBuilding(parent, position, rotation, random, coastal, stoneMaterial, warmStoneMaterial, roofMaterial, glassMaterial);
            }
            building.name = "Building_" + i.ToString("000");
            SetStatic(building);
            if (!HasRoadClearance(building, builder, true))
            {
                if (Application.isPlaying) Object.Destroy(building);
                else Object.DestroyImmediate(building);
            }
        }
    }

    private static void ApplyStoneBuildingMaterials(GameObject building, Material stone, Material warmStone, Material roof, Material glass, System.Random random)
    {
        foreach (MeshRenderer renderer in building.GetComponentsInChildren<MeshRenderer>(true))
        {
            Material[] materials = renderer.sharedMaterials;
            for (int i = 0; i < materials.Length; i++)
            {
                string sourceName = materials[i] != null ? materials[i].name.ToLowerInvariant() : "";
                if (sourceName.Contains("glass") || sourceName.Contains("window")) materials[i] = glass;
                else if (sourceName.Contains("roof") || sourceName.Contains("iron")) materials[i] = roof;
                else materials[i] = random.Next(3) == 0 ? warmStone : stone;
            }
            renderer.sharedMaterials = materials;
        }
    }

    private static GameObject CreateFallbackBuilding(Transform parent, Vector3 position, Quaternion rotation, System.Random random, bool coastal, Material stone, Material warmStone, Material roof, Material glass)
    {
        GameObject building = new GameObject("Building_Fallback");
        building.transform.SetParent(parent, false);
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> walls = new List<int>();
        List<int> roofs = new List<int>();
        List<int> windows = new List<int>();
        float width = TrackMeshBuilder.RandomRange(random, 5f, 8f);
        float depth = TrackMeshBuilder.RandomRange(random, 5f, 9f);
        float height = TrackMeshBuilder.RandomRange(random, coastal ? 7f : 4f, coastal ? 13f : 6f);
        TrackMeshBuilder.AddBox(vertices, uvs, walls, position + Vector3.up * height * 0.5f, rotation, new Vector3(width, height, depth));
        TrackMeshBuilder.AddBox(vertices, uvs, roofs, position + Vector3.up * (height + 0.2f), rotation, new Vector3(width + 0.5f, 0.4f, depth + 0.5f));
        int floors = Mathf.Max(1, Mathf.FloorToInt(height / 3f));
        for (int floor = 0; floor < floors; floor++)
        {
            for (int column = -1; column <= 1; column++)
            {
                Vector3 windowPosition = position + rotation * new Vector3(column * width * 0.27f, 1.8f + floor * 2.8f, -depth * 0.51f);
                TrackMeshBuilder.AddBox(vertices, uvs, windows, windowPosition, rotation, new Vector3(0.8f, 1.2f, 0.08f));
            }
        }
        Mesh mesh = TrackMeshBuilder.Create("StoneBuilding", vertices, uvs, new[] { walls, roofs, windows }, new[] { stone, roof, glass });
        TrackMeshBuilder.CreateObject("BuildingMesh", mesh, new[] { random.Next(2) == 0 ? stone : warmStone, roof, glass }, building.transform, true, true);
        return building;
    }

    private static void CreateHorizon(Transform parent, TrackEnvironmentProfile profile, Vector3 center, System.Random random)
    {
        Material[] materials =
        {
            RaceMaterialFactory.CreateUnlit("HorizonNear", profile.horizonNear),
            RaceMaterialFactory.CreateUnlit("HorizonMiddle", profile.horizonMiddle),
            RaceMaterialFactory.CreateUnlit("HorizonFar", profile.horizonFar)
        };
        for (int layer = 0; layer < Mathf.Clamp(profile.horizonLayerCount, 1, 5); layer++)
        {
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> triangles = new List<int>();
            int segments = 128;
            float radius = profile.horizonRadius + layer * profile.horizonSpacing;
            float baseHeight = profile.seaEnabled ? profile.seaLevel - 2f : -8f;
            float phase = random.Next(0, 360) * Mathf.Deg2Rad;
            for (int i = 0; i <= segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                float noise = 0.72f + Mathf.Sin(angle * 5f + phase) * 0.16f + Mathf.Sin(angle * 11f - phase) * 0.1f;
                float wave = Mathf.Sin(angle * 5f + layer * 1.7f) * 0.16f + Mathf.Sin(angle * 11f - layer) * 0.08f;
                float height = profile.horizonHeight * (1f - layer * 0.13f) * (noise + wave);
                Vector3 direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 bottom = center + direction * radius + Vector3.up * baseHeight;
                Vector3 top = bottom + Vector3.up * height;
                vertices.Add(bottom);
                vertices.Add(top);
                uvs.Add(new Vector2(i / (float)segments, 0f));
                uvs.Add(new Vector2(i / (float)segments, 1f));
                if (i < segments)
                {
                    int a = i * 2;
                    triangles.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 });
                }
            }
            Material material = materials[layer % materials.Length];
            material.SetFloat("_Cull", 0f);
            Mesh mesh = TrackMeshBuilder.Create("HorizonLayer" + layer, vertices, uvs, new[] { triangles }, new[] { material });
            TrackMeshBuilder.CreateObject("Horizon_" + layer, mesh, new[] { material }, parent, false, false);
        }
    }

    private static void CreateDistantGround(Transform parent, Vector3 center, Material groundMaterial)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();
        float size = 2600f;
        float y = -6.2f;
        Vector3 a = center + new Vector3(-size, y, -size);
        Vector3 b = center + new Vector3(-size, y, size);
        Vector3 c = center + new Vector3(size, y, size);
        Vector3 d = center + new Vector3(size, y, -size);
        TrackMeshBuilder.AddQuad(vertices, uvs, triangles, a, b, c, d, -size * 0.05f, -size * 0.05f, size * 0.1f, size * 0.1f);
        Mesh mesh = TrackMeshBuilder.Create("DistantGround", vertices, uvs, new[] { triangles }, new[] { groundMaterial });
        TrackMeshBuilder.CreateObject("DistantGround", mesh, new[] { groundMaterial }, parent, false, false);
    }

    private static void CreateSea(Transform parent, TrackEnvironmentProfile profile, Vector3 center, Material seaMaterial)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();
        float size = 2600f;
        Vector3 a = center + new Vector3(-size, profile.seaLevel, -size);
        Vector3 b = center + new Vector3(-size, profile.seaLevel, size);
        Vector3 c = center + new Vector3(size, profile.seaLevel, size);
        Vector3 d = center + new Vector3(size, profile.seaLevel, -size);
        TrackMeshBuilder.AddQuad(vertices, uvs, triangles, a, b, c, d, -size * 0.05f, -size * 0.05f, size * 0.1f, size * 0.1f);
        Mesh mesh = TrackMeshBuilder.Create("Sea", vertices, uvs, new[] { triangles }, new[] { seaMaterial });
        TrackMeshBuilder.CreateObject("Sea", mesh, new[] { seaMaterial }, parent, true, false);
    }

    private static void CreateHarbor(Transform parent, TrackEnvironmentProfile profile, IReadOnlyList<Vector3> samples, TrackTerrainBuilder terrain, System.Random random, Material stone, Material wood, Material sail, Material metal, Material lamp)
    {
        Vector3 southern = samples[0];
        for (int i = 1; i < samples.Count; i++)
            if (samples[i].z < southern.z) southern = samples[i];
        float harborZ = profile.coastlineZ - 4f;
        float harborX = southern.x;
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> masonry = new List<int>();
        List<int> timber = new List<int>();
        List<int> sails = new List<int>();
        List<int> metals = new List<int>();
        List<int> lamps = new List<int>();
        TrackMeshBuilder.AddBox(vertices, uvs, masonry, new Vector3(harborX, profile.seaLevel + 0.45f, harborZ + 8f), Quaternion.identity, new Vector3(105f, 1.2f, 16f));
        TrackMeshBuilder.AddBox(vertices, uvs, masonry, new Vector3(harborX - 38f, profile.seaLevel + 0.25f, harborZ - 34f), Quaternion.identity, new Vector3(12f, 1.4f, 72f));
        TrackMeshBuilder.AddBox(vertices, uvs, masonry, new Vector3(harborX + 38f, profile.seaLevel + 0.25f, harborZ - 34f), Quaternion.identity, new Vector3(12f, 1.4f, 72f));
        for (int i = 0; i < 5; i++)
        {
            float x = harborX - 30f + i * 15f;
            TrackMeshBuilder.AddBox(vertices, uvs, timber, new Vector3(x, profile.seaLevel + 0.85f, harborZ - 18f), Quaternion.identity, new Vector3(2.2f, 0.35f, 32f));
            TrackMeshBuilder.AddCylinder(vertices, uvs, metals, new Vector3(x, profile.seaLevel + 1.1f, harborZ - 5f), 0.18f, 0.18f, 1.1f, 8, Quaternion.identity);
            if (i % 2 == 0)
            {
                float boatZ = harborZ - 28f - random.Next(0, 8);
                Quaternion rotation = Quaternion.Euler(0f, random.Next(-25, 25), 0f);
                TrackMeshBuilder.AddBox(vertices, uvs, timber, new Vector3(x + 4f, profile.seaLevel + 0.45f, boatZ), rotation, new Vector3(2.6f, 0.65f, 6.5f));
                TrackMeshBuilder.AddCylinder(vertices, uvs, metals, new Vector3(x + 4f, profile.seaLevel + 3.7f, boatZ), 0.07f, 0.05f, 6.5f, 6, Quaternion.identity);
                TrackMeshBuilder.AddTriangle(vertices, uvs, sails, new Vector3(x + 4f, profile.seaLevel + 6.5f, boatZ), new Vector3(x + 4f, profile.seaLevel + 1.6f, boatZ), new Vector3(x + 4f, profile.seaLevel + 1.6f, boatZ + 3.2f));
            }
        }
        Vector3 lighthousePosition = new Vector3(harborX + 38f, profile.seaLevel + 0.9f, harborZ - 68f);
        TrackMeshBuilder.AddCylinder(vertices, uvs, masonry, lighthousePosition + Vector3.up * 5f, 2.2f, 1.5f, 10f, 12, Quaternion.identity);
        TrackMeshBuilder.AddCylinder(vertices, uvs, metals, lighthousePosition + Vector3.up * 10.6f, 1.7f, 1.7f, 1.3f, 12, Quaternion.identity);
        TrackMeshBuilder.AddCylinder(vertices, uvs, lamps, lighthousePosition + Vector3.up * 10.7f, 1.25f, 1.25f, 0.9f, 12, Quaternion.identity);
        Mesh mesh = TrackMeshBuilder.Create("Harbor", vertices, uvs, new[] { masonry, timber, sails, metals, lamps }, new[] { stone, wood, sail, metal, lamp });
        TrackMeshBuilder.CreateObject("Harbor", mesh, new[] { stone, wood, sail, metal, lamp }, parent, true, true);
    }

    public static void BuildSections(Transform parent, TrackEnvironmentProfile profile, TrackBuilder builder, TrackTerrainBuilder terrain, System.Random random, Material bannerMaterial, Material metalMaterial, Material woodMaterial, Material stoneMaterial, Material warmStoneMaterial, Material roofMaterial, Material glassMaterial)
    {
        TrackData.TrackSection[] sections = builder.EffectiveSections;
        foreach (TrackData.TrackSection section in sections)
        {
            if (section == null) continue;
            float start = Mathf.Repeat(section.startProgress, 1f) * builder.TrackLength;
            if (builder.IsStartSafeZone(builder.PointAtDistance(start))) continue;
            switch (section.kind)
            {
                case TrackData.TrackSectionKind.Chikane:
                    CreateChikaneSection(parent, profile, builder, section, start, metalMaterial, bannerMaterial);
                    break;
                case TrackData.TrackSectionKind.StoneBridge:
                    CreateStoneBridge(parent, profile, builder, terrain, section, start, stoneMaterial);
                    break;
                case TrackData.TrackSectionKind.StoneSection:
                    CreateStoneSection(parent, builder, section, start, stoneMaterial);
                    break;
                case TrackData.TrackSectionKind.RangerStation:
                    CreateRangerStation(parent, builder, terrain, section, start, random, stoneMaterial, warmStoneMaterial, roofMaterial, glassMaterial);
                    break;
                case TrackData.TrackSectionKind.Grandstand:
                    CreateGrandstand(parent, builder, terrain, section, start, woodMaterial);
                    break;
            }
        }
    }

    private static void CreateChikaneSection(Transform parent, TrackEnvironmentProfile profile, TrackBuilder builder, TrackData.TrackSection section, float start, Material metalMaterial, Material bannerMaterial)
    {
        float half = builder.track.roadWidth * 0.5f;
        float length = Mathf.Clamp(section.lengthMeters, 36f, Mathf.Min(120f, builder.TrackLength * 0.25f));
        float amplitude = Mathf.Clamp(section.amplitudeMeters, 0.5f, Mathf.Min(2.2f, half + profile.shoulderWidth - 0.75f));
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> rails = new List<int>();
        List<int> markers = new List<int>();
        float step = 3.5f;
        float previousDistance = start;
        for (float distance = start; distance <= start + length + step; distance += step)
        {
            float currentDistance = Mathf.Min(distance, start + length);
            float t = Mathf.Clamp01((currentDistance - start) / length);
            float envelope = SmoothStep(0f, 0.12f, t) * (1f - SmoothStep(0.88f, 1f, t));
            float wave = Mathf.Sin(t * Mathf.PI * 4f) * envelope;
            builder.FrameAtDistance(currentDistance, out Vector3 point, out Vector3 tangent, out Vector3 right, out Vector3 normal);
            float roadOffset = builder.RoadHeightOffsetAtDistance(currentDistance);
            if (distance > start)
            {
                builder.FrameAtDistance(previousDistance, out Vector3 previousPoint, out _, out Vector3 previousRight, out Vector3 previousNormal);
                float previousT = Mathf.Clamp01((previousDistance - start) / length);
                float previousEnvelope = SmoothStep(0f, 0.12f, previousT) * (1f - SmoothStep(0.88f, 1f, previousT));
                float previousWave = Mathf.Sin(previousT * Mathf.PI * 4f) * previousEnvelope;
                float previousOffset = builder.RoadHeightOffsetAtDistance(previousDistance);
                for (int side = -1; side <= 1; side += 2)
                {
                    float inset = amplitude * (0.5f + side * wave * 0.5f);
                    float previousInset = amplitude * (0.5f + side * previousWave * 0.5f);
                    float lateral = side * (half + profile.shoulderWidth + 0.35f - inset);
                    float previousLateral = side * (half + profile.shoulderWidth + 0.35f - previousInset);
                    for (int height = 0; height < 2; height++)
                    {
                        float y = 0.5f + height * 0.48f;
                        TrackMeshBuilder.AddBeam(vertices, uvs, rails,
                            previousPoint + previousRight * previousLateral + previousNormal * (previousOffset + y),
                            point + right * lateral + normal * (roadOffset + y), 0.11f, 0.12f, normal);
                    }
                }
            }
            if (Mathf.RoundToInt((currentDistance - start) / step) % 2 == 0)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    float inset = amplitude * (0.5f + side * wave * 0.5f);
                    float lateral = side * (half + profile.shoulderWidth + 0.35f - inset);
                    TrackMeshBuilder.AddBox(vertices, uvs, rails, point + right * lateral + normal * (roadOffset + 0.48f),
                        Quaternion.LookRotation(tangent, normal), new Vector3(0.14f, 1.08f, 0.14f));
                }
            }
            previousDistance = currentDistance;
            if (currentDistance >= start + length) break;
        }
        for (float distance = start + 3f; distance < start + length; distance += 11f)
        {
            builder.FrameAtDistance(distance, out Vector3 point, out Vector3 tangent, out Vector3 right, out Vector3 normal);
            float offset = builder.RoadHeightOffsetAtDistance(distance);
            TrackMeshBuilder.AddBox(vertices, uvs, markers, point + right * (half + 0.25f) + normal * (offset + 0.72f),
                Quaternion.LookRotation(tangent, normal), new Vector3(0.08f, 0.34f, 1.1f));
        }
        Mesh mesh = TrackMeshBuilder.Create("TrackSection_" + section.id, vertices, uvs, new[] { rails, markers }, new[] { metalMaterial, bannerMaterial });
        TrackMeshBuilder.CreateObject("TrackSection_" + section.id, mesh, new[] { metalMaterial, bannerMaterial }, parent, true, true);
    }

    private static void CreateStoneBridge(Transform parent, TrackEnvironmentProfile profile, TrackBuilder builder, TrackTerrainBuilder terrain, TrackData.TrackSection section, float start, Material stoneMaterial)
    {
        float half = builder.track.roadWidth * 0.5f;
        float shoulder = profile.shoulderWidth;
        float length = Mathf.Clamp(section.lengthMeters, 24f, Mathf.Min(72f, builder.TrackLength * 0.2f));
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();
        float step = 2.5f;
        for (float distance = start; distance < start + length; distance += step)
        {
            float current = Mathf.Min(distance + step, start + length);
            float segmentLength = current - distance;
            float middle = (distance + current) * 0.5f;
            builder.FrameAtDistance(middle, out Vector3 point, out Vector3 tangent, out Vector3 right, out Vector3 normal);
            float offset = builder.RoadHeightOffsetAtDistance(middle);
            Quaternion rotation = Quaternion.LookRotation(tangent, normal);
            TrackMeshBuilder.AddBox(vertices, uvs, triangles, point + normal * (offset - 0.14f), rotation,
                new Vector3(builder.track.roadWidth + shoulder * 2f + 0.8f, 0.28f, segmentLength + 0.06f));
            for (int side = -1; side <= 1; side += 2)
            {
                float lateral = side * (half + shoulder + 0.5f);
                TrackMeshBuilder.AddBox(vertices, uvs, triangles, point + right * lateral + normal * (offset + 0.36f), rotation,
                    new Vector3(0.4f, 0.82f, segmentLength + 0.04f));
            }
        }
        int archCount = Mathf.Max(2, Mathf.RoundToInt(length / 15f));
        float archSpan = length / archCount;
        for (int arch = 0; arch < archCount; arch++)
        {
            float archCenter = start + archSpan * (arch + 0.5f);
            Vector3 previous = Vector3.zero;
            for (int i = 0; i <= 10; i++)
            {
                float t = i / 10f;
                float distance = archCenter - archSpan * 0.46f + archSpan * 0.92f * t;
                builder.FrameAtDistance(distance, out Vector3 point, out Vector3 tangent, out Vector3 right, out Vector3 normal);
                float deckOffset = builder.RoadHeightOffsetAtDistance(distance) - 0.34f;
                float archOffset = deckOffset - (0.9f + Mathf.Sin(t * Mathf.PI) * 2.3f);
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 current = point + right * side * (half + shoulder * 0.55f) + normal * archOffset;
                    if (i > 0)
                    {
                        Vector3 previousSide = previous + right * side * (half + shoulder * 0.55f);
                        TrackMeshBuilder.AddBeam(vertices, uvs, triangles, previousSide, current, 0.52f, 0.46f, normal);
                    }
                    if (i % 2 == 0)
                    {
                        Vector3 top = point + right * side * (half + shoulder * 0.55f) + normal * deckOffset;
                        TrackMeshBuilder.AddBox(vertices, uvs, triangles, (current + top) * 0.5f, Quaternion.LookRotation(tangent, normal),
                            new Vector3(0.42f, Mathf.Max(0.2f, Vector3.Dot(top - current, normal)), 0.42f));
                    }
                }
                previous = point;
            }
        }
        for (float distance = start; distance <= start + length; distance += length)
        {
            builder.FrameAtDistance(distance, out Vector3 point, out Vector3 tangent, out Vector3 right, out Vector3 normal);
            float roadOffset = builder.RoadHeightOffsetAtDistance(distance);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 position = point + right * side * (half + shoulder + 0.5f);
                float bottom = Mathf.Min(terrain.SampleHeight(position), point.y - 0.5f);
                TrackMeshBuilder.AddBox(vertices, uvs, triangles, new Vector3(position.x, (bottom + point.y + roadOffset + 0.75f) * 0.5f, position.z),
                    Quaternion.LookRotation(tangent, Vector3.up), new Vector3(0.85f, point.y + roadOffset + 0.75f - bottom, 1.1f));
            }
        }
        Mesh mesh = TrackMeshBuilder.Create("TrackSection_" + section.id, vertices, uvs, new[] { triangles }, new[] { stoneMaterial });
        TrackMeshBuilder.CreateObject("TrackSection_" + section.id, mesh, new[] { stoneMaterial }, parent, true, true);
    }

    private static void CreateStoneSection(Transform parent, TrackBuilder builder, TrackData.TrackSection section, float start, Material stoneMaterial)
    {
        float half = builder.track.roadWidth * 0.5f;
        float length = Mathf.Clamp(section.lengthMeters, 24f, Mathf.Min(110f, builder.TrackLength * 0.25f));
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();
        for (float distance = start; distance < start + length; distance += 3f)
        {
            float current = Mathf.Min(distance + 3f, start + length);
            float middle = (distance + current) * 0.5f;
            builder.FrameAtDistance(middle, out Vector3 point, out Vector3 tangent, out Vector3 right, out Vector3 normal);
            float offset = builder.RoadHeightOffsetAtDistance(middle);
            Quaternion rotation = Quaternion.LookRotation(tangent, normal);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 lateral = right * side * (half + 1.55f);
                TrackMeshBuilder.AddBox(vertices, uvs, triangles, point + lateral + normal * (offset + 0.22f), rotation,
                    new Vector3(0.42f, 0.54f, current - distance + 0.03f));
                if (Mathf.RoundToInt((distance - start) / 3f) % 2 == 0)
                    TrackMeshBuilder.AddBox(vertices, uvs, triangles, point + lateral + normal * (offset + 0.62f), rotation,
                        new Vector3(0.56f, 0.18f, 0.56f));
            }
        }
        Mesh mesh = TrackMeshBuilder.Create("TrackSection_" + section.id, vertices, uvs, new[] { triangles }, new[] { stoneMaterial });
        TrackMeshBuilder.CreateObject("TrackSection_" + section.id, mesh, new[] { stoneMaterial }, parent, true, true);
    }

    private static void CreateRangerStation(Transform parent, TrackBuilder builder, TrackTerrainBuilder terrain, TrackData.TrackSection section, float start, System.Random random, Material stoneMaterial, Material warmStoneMaterial, Material roofMaterial, Material glassMaterial)
    {
        builder.FrameAtDistance(start, out Vector3 point, out Vector3 tangent, out Vector3 right, out Vector3 normal);
        float side = section.side < 0f ? -1f : 1f;
        Vector3 position = point + right * side * (builder.track.roadWidth * 0.5f + 16f);
        position.y = terrain.SampleHeight(position);
        GameObject root = new GameObject("TrackSection_" + section.id);
        root.transform.SetParent(parent, false);
        GameObject prefab = Resources.Load<GameObject>("Environment/ReleaseKit/Building_Log_Cabin_Detailed_01");
        GameObject building;
        if (prefab != null)
        {
            building = InstantiateAsset(prefab, position, Quaternion.LookRotation(-right * side, Vector3.up), root.transform);
            building.transform.localScale = Vector3.one * 1.15f;
            GroundInstance(building, position.y);
        }
        else building = CreateFallbackBuilding(root.transform, position, Quaternion.LookRotation(-right * side, Vector3.up), random, false, stoneMaterial, warmStoneMaterial, roofMaterial, glassMaterial);
        building.name = "RangerStation";
        SetStatic(building);
        if (!HasRoadClearance(building, builder, true))
        {
            if (Application.isPlaying) Object.Destroy(root);
            else Object.DestroyImmediate(root);
        }
    }

    private static void CreateGrandstand(Transform parent, TrackBuilder builder, TrackTerrainBuilder terrain, TrackData.TrackSection section, float start, Material woodMaterial)
    {
        builder.FrameAtDistance(start, out Vector3 point, out Vector3 tangent, out Vector3 right, out Vector3 normal);
        float side = section.side < 0f ? -1f : 1f;
        Vector3 position = point + right * side * (builder.track.roadWidth * 0.5f + 14f);
        position.y = terrain.SampleHeight(position);
        Quaternion rotation = Quaternion.LookRotation(-right * side, Vector3.up);
        Vector3 away = rotation * Vector3.back;
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();
        for (int row = 0; row < 4; row++)
        {
            Vector3 center = position + away * (0.8f + row * 1.05f) + Vector3.up * (0.35f + row * 0.62f);
            TrackMeshBuilder.AddBox(vertices, uvs, triangles, center, rotation, new Vector3(18f, 0.42f, 1.35f));
        }
        for (float x = -8f; x <= 8f; x += 4f)
        {
            Vector3 post = position + rotation * new Vector3(x, 2.15f, 0f) + away * 2.4f;
            TrackMeshBuilder.AddBox(vertices, uvs, triangles, post, rotation, new Vector3(0.18f, 4.3f, 0.18f));
        }
        TrackMeshBuilder.AddBox(vertices, uvs, triangles, position + away * 2.1f + Vector3.up * 4.45f, rotation, new Vector3(19f, 0.18f, 6f));
        Mesh mesh = TrackMeshBuilder.Create("TrackSection_" + section.id, vertices, uvs, new[] { triangles }, new[] { woodMaterial });
        TrackMeshBuilder.CreateObject("TrackSection_" + section.id, mesh, new[] { woodMaterial }, parent, true, true);
    }

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        float t = Mathf.Clamp01((value - edge0) / Mathf.Max(0.0001f, edge1 - edge0));
        return t * t * (3f - 2f * t);
    }

    public static void GroundInstance(GameObject instance, float groundHeight)
    {
        Bounds bounds = GetBounds(instance);
        if (float.IsNaN(bounds.extents.sqrMagnitude)) return;
        instance.transform.position += Vector3.up * (groundHeight + 0.02f - bounds.min.y);
    }

    public static Bounds GetBounds(GameObject instance)
    {
        MeshRenderer[] renderers = instance.GetComponentsInChildren<MeshRenderer>();
        if (renderers.Length == 0) return new Bounds(instance.transform.position, Vector3.zero);
        Bounds bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
        return bounds;
    }

    public static GameObject InstantiateAsset(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
    {
        return prefab == null ? null : Object.Instantiate(prefab, position, rotation * prefab.transform.rotation, parent);
    }

    public static void SetStatic(GameObject instance, bool addCollider = true)
    {
        foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true)) DestroyCollider(collider);
        foreach (Transform child in instance.GetComponentsInChildren<Transform>(true)) child.gameObject.isStatic = true;
        if (!addCollider) return;
        HashSet<MeshRenderer> collisionRenderers = null;
        LODGroup lodGroup = instance.GetComponentInChildren<LODGroup>();
        if (lodGroup != null)
        {
            LOD[] lods = lodGroup.GetLODs();
            if (lods.Length > 0 && lods[0].renderers != null)
            {
                collisionRenderers = new HashSet<MeshRenderer>();
                foreach (Renderer lodRenderer in lods[0].renderers)
                    if (lodRenderer is MeshRenderer meshRenderer) collisionRenderers.Add(meshRenderer);
            }
        }
        foreach (MeshRenderer renderer in instance.GetComponentsInChildren<MeshRenderer>(true))
        {
            if (collisionRenderers != null && !collisionRenderers.Contains(renderer)) continue;
            if (!IsSolidDecorName(renderer.name)) continue;
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null) continue;
            BoxCollider collider = renderer.gameObject.AddComponent<BoxCollider>();
            collider.center = filter.sharedMesh.bounds.center;
            collider.size = filter.sharedMesh.bounds.size;
        }
    }

    private static void DestroyCollider(Collider collider)
    {
        if (collider == null) return;
        if (Application.isPlaying) Object.Destroy(collider);
        else Object.DestroyImmediate(collider);
    }

    private static bool IsSolidDecorName(string name)
    {
        string value = name.ToLowerInvariant();
        if (value.Contains("wire") || value.Contains("cloud") || value.Contains("crown") || value.Contains("canopy")
            || value.Contains("leaf") || value.Contains("leaves") || value.Contains("needle") || value.Contains("bough")
            || value.Contains("snow") || value.Contains("banner") || value.Contains("checker") || value.Contains("pennant")
            || value.Contains("label") || value.Contains("flag") || value.Contains("insulator")) return false;
        bool tree = value.Contains("tree_");
        bool arch = value.Contains("start_arch") || value.Contains("startarch");
        bool telegraph = value.Contains("telegraph");
        if (arch) return value.Contains("pillar");
        if (telegraph) return value.Contains("pole") || value.Contains("crossarm");
        if (tree) return value.Contains("trunk") || value.Contains("branch") || value.Contains("limb") || value.Contains("bark");
        return true;
    }

    public static bool HasRoadClearance(GameObject instance, TrackBuilder builder, bool includeVisualBounds)
    {
        List<Bounds> bounds = new List<Bounds>();
        if (includeVisualBounds)
        {
            foreach (MeshRenderer renderer in instance.GetComponentsInChildren<MeshRenderer>(true)) bounds.Add(renderer.bounds);
        }
        else
        {
            foreach (Collider collider in instance.GetComponentsInChildren<Collider>(true))
                if (collider.enabled) bounds.Add(collider.bounds);
        }
        foreach (Bounds candidate in bounds)
        {
            float radius = new Vector2(candidate.extents.x, candidate.extents.z).magnitude;
            if (!builder.HasRoadClearance(candidate.center, radius)) return false;
        }
        return true;
    }

    private static void RegisterTreeCulling(GameObject tree)
    {
        if (tree == null) return;
        cullableTrees.Add(new CullableTree
        {
            root = tree,
            lodGroup = tree.GetComponent<LODGroup>(),
            renderers = tree.GetComponentsInChildren<MeshRenderer>(true)
        });
    }

    public static void UpdateTreeCulling(Vector3 focus, float maximumDistance)
    {
        float distanceSquared = maximumDistance * maximumDistance;
        for (int i = cullableTrees.Count - 1; i >= 0; i--)
        {
            CullableTree tree = cullableTrees[i];
            if (tree.root == null)
            {
                cullableTrees.RemoveAt(i);
                continue;
            }
            bool visible = (tree.root.transform.position - focus).sqrMagnitude <= distanceSquared;
            if (tree.lodGroup != null) tree.lodGroup.enabled = visible;
            else foreach (MeshRenderer renderer in tree.renderers) if (renderer != null) renderer.enabled = visible;
        }
    }

    public static void ClearCulling() => cullableTrees.Clear();
}
