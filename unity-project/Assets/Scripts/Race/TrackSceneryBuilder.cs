using System.Collections.Generic;
using UnityEngine;

public static class TrackSceneryBuilder
{
    public static void Build(Transform parent, TrackEnvironmentProfile profile, IReadOnlyList<Vector3> samples, TrackTerrainBuilder terrain, System.Random random, float halfRoadWidth, Material snowMaterial, Material rockMaterial, Material stoneMaterial, Material warmStoneMaterial, Material roofMaterial, Material glassMaterial, Material woodMaterial, Material foliageMaterial, Material sailMaterial, Material metalMaterial, Material lampMaterial, Material seaMaterial, Material groundMaterial)
    {
        CreateTrees(parent, profile, samples, terrain, random, halfRoadWidth, snowMaterial, woodMaterial, foliageMaterial);
        CreateRocks(parent, profile, samples, terrain, random, halfRoadWidth, rockMaterial);
        CreateBuildings(parent, profile, samples, terrain, random, halfRoadWidth, stoneMaterial, warmStoneMaterial, roofMaterial, glassMaterial);
        CreateHorizon(parent, profile, terrain.Center, random);
        if (profile.seaEnabled) CreateSea(parent, profile, terrain.Center, seaMaterial);
        else CreateDistantGround(parent, terrain.Center, groundMaterial);
        if (profile.harborEnabled) CreateHarbor(parent, profile, samples, terrain, random, stoneMaterial, woodMaterial, sailMaterial, metalMaterial, lampMaterial);
    }

    private static void CreateTrees(Transform parent, TrackEnvironmentProfile profile, IReadOnlyList<Vector3> samples, TrackTerrainBuilder terrain, System.Random random, float halfRoadWidth, Material snowMaterial, Material woodMaterial, Material foliageMaterial)
    {
        List<Vector3> occupied = new List<Vector3>();
        for (int i = 0; i < profile.treeCount; i++)
        {
            Vector3 position;
            if (!TryFindSceneryPosition(samples, terrain, random, halfRoadWidth + profile.shoulderWidth + profile.barrierOffset + 1.2f, profile.sceneryOffsetMin, profile.sceneryOffsetMax, occupied, 2.4f, profile.seaEnabled ? profile.coastlineZ + 5f : float.NegativeInfinity, out position)) continue;
            position.y = terrain.SampleHeight(position);
            string resource = profile.treeResources != null && profile.treeResources.Length > 0 ? profile.treeResources[random.Next(profile.treeResources.Length)] : "Pine";
            GameObject prefab = Resources.Load<GameObject>($"Environment/{resource}");
            GameObject tree;
            if (prefab != null)
            {
                tree = Object.Instantiate(prefab, position, Quaternion.Euler(0f, random.Next(0, 360), 0f), parent);
                float scale = TrackMeshBuilder.RandomRange(random, profile.treeScaleMin, profile.treeScaleMax);
                tree.transform.localScale = Vector3.one * scale;
                GroundInstance(tree, position.y);
            }
            else
            {
                tree = CreateFallbackTree(parent, position, random, resource == "Birch", woodMaterial, foliageMaterial);
            }
            tree.name = "Tree_" + resource + "_" + i.ToString("000");
            occupied.Add(position);
            SetStatic(tree);
            if (profile.treeSnowChance > 0f && random.NextDouble() < profile.treeSnowChance)
                AddSnowCap(tree, random, snowMaterial, resource != "Birch");
        }
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

    private static bool TryFindSceneryPosition(IReadOnlyList<Vector3> samples, TrackTerrainBuilder terrain, System.Random random, float minimumRoadDistance, float minimumOffset, float maximumOffset, List<Vector3> occupied, float occupiedSpacing, float minimumZ, out Vector3 result)
    {
        result = Vector3.zero;
        for (int attempt = 0; attempt < 24; attempt++)
        {
            int sample = random.Next(samples.Count);
            Vector3 point = samples[sample];
            Vector3 tangent = samples[(sample + 1) % samples.Count] - point;
            Vector3 right = Vector3.Cross(Vector3.up, tangent.normalized).normalized;
            int side = random.Next(2) == 0 ? -1 : 1;
            Vector3 candidate = point + right * side * TrackMeshBuilder.RandomRange(random, minimumOffset, maximumOffset);
            float roadHeight;
            float roadDistance = terrain.DistanceToRoad(candidate, out roadHeight);
            if (roadDistance < minimumRoadDistance || candidate.z < minimumZ) continue;
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

    private static void CreateRocks(Transform parent, TrackEnvironmentProfile profile, IReadOnlyList<Vector3> samples, TrackTerrainBuilder terrain, System.Random random, float halfRoadWidth, Material rockMaterial)
    {
        if (profile.rockCount <= 0) return;
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();
        List<Vector3> occupied = new List<Vector3>();
        for (int i = 0; i < profile.rockCount; i++)
        {
            Vector3 position;
            if (!TryFindSceneryPosition(samples, terrain, random, halfRoadWidth + profile.shoulderWidth + 1.5f, 8f, 42f, occupied, 3f, float.NegativeInfinity, out position)) continue;
            occupied.Add(position);
            position.y = terrain.SampleHeight(position);
            Vector3 scale = new Vector3(TrackMeshBuilder.RandomRange(random, 1.2f, 4.8f), TrackMeshBuilder.RandomRange(random, 1.5f, 7.5f), TrackMeshBuilder.RandomRange(random, 1.2f, 4.2f));
            TrackMeshBuilder.AddRock(vertices, uvs, triangles, position + Vector3.up * scale.y * 0.28f, scale, profile.seed + i * 31);
        }
        Mesh mesh = TrackMeshBuilder.Create("AlpineRocks", vertices, uvs, new[] { triangles }, new[] { rockMaterial });
        TrackMeshBuilder.CreateObject("Rocks", mesh, new[] { rockMaterial }, parent, true, true);
    }

    private static void CreateBuildings(Transform parent, TrackEnvironmentProfile profile, IReadOnlyList<Vector3> samples, TrackTerrainBuilder terrain, System.Random random, float halfRoadWidth, Material stoneMaterial, Material warmStoneMaterial, Material roofMaterial, Material glassMaterial)
    {
        GameObject prefab = Resources.Load<GameObject>($"Environment/{profile.buildingResource}");
        List<Vector3> occupied = new List<Vector3>();
        for (int i = 0; i < profile.buildingCount; i++)
        {
            Vector3 position;
            if (!TryFindSceneryPosition(samples, terrain, random, halfRoadWidth + profile.shoulderWidth + 5f, 12f, 27f, occupied, 6f, profile.seaEnabled ? profile.coastlineZ + 8f : float.NegativeInfinity, out position)) continue;
            occupied.Add(position);
            position.y = terrain.SampleHeight(position);
            Vector3 point = samples[0];
            float nearest = float.MaxValue;
            foreach (Vector3 sample in samples)
            {
                float candidate = (sample - position).sqrMagnitude;
                if (candidate < nearest)
                {
                    nearest = candidate;
                    point = sample;
                }
            }
            int nearestIndex = 0;
            for (int index = 1; index < samples.Count; index++)
            {
                if ((samples[index] - position).sqrMagnitude < nearest)
                {
                    nearest = (samples[index] - position).sqrMagnitude;
                    nearestIndex = index;
                }
            }
            point = samples[nearestIndex];
            Vector3 tangent = samples[(nearestIndex + 1) % samples.Count] - point;
            Vector3 right = Vector3.Cross(Vector3.up, tangent.normalized).normalized;
            int side = Vector3.Dot(position - point, right) < 0f ? -1 : 1;
            Quaternion rotation = Quaternion.LookRotation(-right * side, Vector3.up);
            GameObject building;
            if (prefab != null)
            {
                building = Object.Instantiate(prefab, position, rotation, parent);
                float width = TrackMeshBuilder.RandomRange(random, 0.85f, 1.25f);
                float height = profile.seaEnabled ? TrackMeshBuilder.RandomRange(random, 1.25f, 2.15f) : TrackMeshBuilder.RandomRange(random, 0.9f, 1.2f);
                building.transform.localScale = new Vector3(width, height, width);
                GroundInstance(building, position.y);
                if (profile.seaEnabled) ApplyStoneBuildingMaterials(building, stoneMaterial, warmStoneMaterial, roofMaterial, glassMaterial, random);
            }
            else
            {
                building = CreateFallbackBuilding(parent, position, rotation, random, profile.seaEnabled, stoneMaterial, warmStoneMaterial, roofMaterial, glassMaterial);
            }
            building.name = "Building_" + i.ToString("000");
            SetStatic(building);
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

    public static void SetStatic(GameObject instance)
    {
        instance.isStatic = true;
        for (int i = 0; i < instance.transform.childCount; i++) SetStatic(instance.transform.GetChild(i).gameObject);
    }
}
