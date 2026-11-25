using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class TrackBuilder : MonoBehaviour
{
    public TrackData track;
    public int sampleCount = 220;
    public int checkpointCount = 12;
    public List<Vector3> Samples { get; private set; } = new List<Vector3>();
    public List<Vector3> Tangents { get; private set; } = new List<Vector3>();
    public float TrackLength { get; private set; }
    public Vector3 StartPoint => Samples.Count > 0 ? Samples[0] : Vector3.zero;
    public Vector3 StartTangent => Tangents.Count > 0 ? Tangents[0] : Vector3.forward;

    private Material roadMaterial;
    private Material groundMaterial;
    private Material curbMaterial;

    public void Build(TrackData data)
    {
        track = data;
        Samples.Clear();
        Tangents.Clear();
        TrackLength = 0f;
        CreateMaterials();
        CreateAtmosphere();
        CreateGround();
        CreateSampledPath();
        CreateRoadMesh();
        CreateCurbs();
        CreateStartGantry();
        CreateCheckpoints();
        CreateScenery();
        CreateRoadsideDetails();
    }

    private void CreateMaterials()
    {
        string roadTexture = track.id == "monaco_finish" ? "road_cobblestone" : "road_dirt";
        roadMaterial = CreateMaterial("RoadMaterial", track.roadColor, 0.18f, roadTexture);
        groundMaterial = CreateMaterial("GroundMaterial", track.groundColor, 0.04f, "road_dirt");
        curbMaterial = CreateMaterial("CurbMaterial", track.curbColor, 0.10f);
    }

    private Material CreateMaterial(string name, Color color, float smoothness, string texturePrefix = null)
    {
        Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.name = name;
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", smoothness);
        if (!string.IsNullOrEmpty(texturePrefix))
        {
            Texture2D baseMap = Resources.Load<Texture2D>($"Textures/{texturePrefix}_basecolor");
            Texture2D normalMap = Resources.Load<Texture2D>($"Textures/{texturePrefix}_normal");
            Texture2D roughnessMap = Resources.Load<Texture2D>($"Textures/{texturePrefix}_roughness");
            if (baseMap != null)
            {
                material.SetTexture("_BaseMap", baseMap);
                material.SetTextureScale("_BaseMap", new Vector2(3f, 24f));
            }
            if (normalMap != null)
            {
                material.SetTexture("_BumpMap", normalMap);
                material.SetFloat("_BumpScale", 0.65f);
            }
            if (roughnessMap != null)
            {
                material.SetFloat("_Metallic", 0f);
                material.SetFloat("_Smoothness", 0.12f);
            }
        }
        return material;
    }

    private void CreateAtmosphere()
    {
        bool alpine = track.id == "alps_pass";
        bool coastal = track.id == "monaco_finish";
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = alpine ? 0.007f : coastal ? 0.0025f : 0.004f;
        RenderSettings.fogColor = alpine ? new Color(0.56f, 0.62f, 0.63f) : coastal ? new Color(0.66f, 0.75f, 0.78f) : new Color(0.48f, 0.55f, 0.52f);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientSkyColor = new Color(0.42f, 0.48f, 0.45f);
        RenderSettings.ambientEquatorColor = new Color(0.30f, 0.34f, 0.28f);
        RenderSettings.ambientGroundColor = new Color(0.16f, 0.15f, 0.11f);
        Light sun = FindAnyObjectByType<Light>();
        if (sun != null)
        {
            sun.color = coastal ? new Color(1f, 0.91f, 0.73f) : new Color(1f, 0.86f, 0.64f);
            sun.intensity = coastal ? 1.35f : 1.15f;
        }
        Camera main = Camera.main;
        if (main != null) main.backgroundColor = RenderSettings.fogColor;

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        Bloom bloom = ScriptableObject.CreateInstance<Bloom>();
        bloom.intensity.value = 0.28f;
        bloom.threshold.value = 1.05f;
        bloom.scatter.value = 0.55f;
        profile.components.Add(bloom);
        ColorAdjustments color = ScriptableObject.CreateInstance<ColorAdjustments>();
        color.postExposure.value = 0.15f;
        color.contrast.value = 12f;
        color.saturation.value = 6f;
        profile.components.Add(color);
        Vignette vignette = ScriptableObject.CreateInstance<Vignette>();
        vignette.intensity.value = 0.18f;
        vignette.smoothness.value = 0.35f;
        profile.components.Add(vignette);
        GameObject volumeObject = new GameObject("AtmosphereVolume");
        volumeObject.transform.SetParent(transform, false);
        Volume volume = volumeObject.AddComponent<Volume>();
        volume.profile = profile;
        volume.isGlobal = true;
    }

    private void CreateGround()
    {
        GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ground.name = "Terrain";
        ground.transform.SetParent(transform, false);
        ground.transform.localPosition = new Vector3(0f, -0.18f, 0f);
        ground.transform.localScale = new Vector3(650f, 0.3f, 650f);
        ground.GetComponent<MeshRenderer>().sharedMaterial = groundMaterial;
        ground.isStatic = true;
    }

    private void CreateSampledPath()
    {
        if (track.waypoints == null || track.waypoints.Length < 4)
            track.waypoints = DefaultLoop();

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleCount;
            Samples.Add(CatmullRom(t));
        }
        for (int i = 0; i < sampleCount; i++)
        {
            Vector3 previous = Samples[(i - 1 + sampleCount) % sampleCount];
            Vector3 next = Samples[(i + 1) % sampleCount];
            Tangents.Add((next - previous).normalized);
            if (i > 0)
                TrackLength += Vector3.Distance(Samples[i - 1], Samples[i]);
        }
        TrackLength += Vector3.Distance(Samples[sampleCount - 1], Samples[0]);
    }

    private Vector3 CatmullRom(float t)
    {
        int count = track.waypoints.Length;
        float scaled = t * count;
        int i = Mathf.FloorToInt(scaled) % count;
        float u = scaled - Mathf.FloorToInt(scaled);
        Vector3 p0 = track.waypoints[(i - 1 + count) % count];
        Vector3 p1 = track.waypoints[i];
        Vector3 p2 = track.waypoints[(i + 1) % count];
        Vector3 p3 = track.waypoints[(i + 2) % count];
        return 0.5f * ((2f * p1) + (-p0 + p2) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u * u + (-p0 + 3f * p1 - 3f * p2 + p3) * u * u * u);
    }

    private Vector3[] DefaultLoop()
    {
        return new[]
        {
            new Vector3(-55f, 0f, 35f), new Vector3(-20f, 0f, 5f), new Vector3(15f, 0f, -30f),
            new Vector3(55f, 0f, -15f), new Vector3(50f, 0f, 35f), new Vector3(10f, 0f, 60f), new Vector3(-40f, 0f, 55f)
        };
    }

    private void CreateRoadMesh()
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();
        for (int i = 0; i <= sampleCount; i++)
        {
            int index = i % sampleCount;
            Vector3 point = Samples[index];
            Vector3 tangent = Tangents[index];
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
            vertices.Add(point + right * track.roadWidth * 0.5f + Vector3.up * 0.03f);
            vertices.Add(point - right * track.roadWidth * 0.5f + Vector3.up * 0.03f);
            float v = i * 0.25f;
            uvs.Add(new Vector2(0f, v));
            uvs.Add(new Vector2(1f, v));
            if (i < sampleCount)
            {
                int a = i * 2;
                triangles.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 });
            }
        }

        Mesh mesh = new Mesh { name = "TrackRoad" };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        GameObject road = new GameObject("Road", typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
        road.transform.SetParent(transform, false);
        road.GetComponent<MeshFilter>().sharedMesh = mesh;
        road.GetComponent<MeshRenderer>().sharedMaterial = roadMaterial;
        road.GetComponent<MeshCollider>().sharedMesh = mesh;
        road.isStatic = true;
    }

    private void CreateCurbs()
    {
        for (int i = 0; i < sampleCount; i += 7)
        {
            Vector3 point = Samples[i];
            Vector3 tangent = Tangents[i];
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
            Quaternion rotation = Quaternion.LookRotation(tangent, Vector3.up);
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject curb = GameObject.CreatePrimitive(PrimitiveType.Cube);
                curb.name = "Curb";
                curb.transform.SetParent(transform, false);
                curb.transform.position = point + right * side * (track.roadWidth * 0.5f + 0.18f) + Vector3.up * 0.09f;
                curb.transform.rotation = rotation;
                curb.transform.localScale = new Vector3(0.32f, 0.16f, 2.8f);
                curb.GetComponent<MeshRenderer>().sharedMaterial = curbMaterial;
                curb.isStatic = true;
            }
        }
    }

    private void CreateStartGantry()
    {
        Vector3 point = StartPoint;
        Vector3 tangent = StartTangent;
        Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
        Quaternion rotation = Quaternion.LookRotation(tangent, Vector3.up);
        for (int side = -1; side <= 1; side += 2)
        {
            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillar.name = "StartPillar";
            pillar.transform.SetParent(transform, false);
            pillar.transform.position = point + right * side * (track.roadWidth * 0.5f + 0.6f) + Vector3.up * 2.5f;
            pillar.transform.rotation = rotation;
            pillar.transform.localScale = new Vector3(0.35f, 5f, 0.35f);
            pillar.GetComponent<MeshRenderer>().sharedMaterial = curbMaterial;
        }
        GameObject banner = GameObject.CreatePrimitive(PrimitiveType.Cube);
        banner.name = "StartBanner";
        banner.transform.SetParent(transform, false);
        banner.transform.position = point + Vector3.up * 4.8f;
        banner.transform.rotation = rotation;
        banner.transform.localScale = new Vector3(track.roadWidth + 1.4f, 0.8f, 0.25f);
        banner.GetComponent<MeshRenderer>().sharedMaterial = roadMaterial;
        TextMesh label = banner.AddComponent<TextMesh>();
        if (label != null)
        {
            label.text = string.IsNullOrEmpty(track.displayName) ? "RUSSO-BALT" : track.displayName.ToUpperInvariant();
            label.fontSize = 48;
            label.color = UITheme.Cream;
            label.alignment = TextAlignment.Center;
            label.anchor = TextAnchor.MiddleCenter;
        }
        for (int i = 0; i < 16; i++)
        {
            GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.name = "StartLineTile";
            tile.transform.SetParent(transform, false);
            tile.transform.position = point + right * (-track.roadWidth * 0.48f + i * track.roadWidth * 0.96f / 15f) + Vector3.up * 0.045f;
            tile.transform.rotation = rotation;
            tile.transform.localScale = new Vector3(track.roadWidth * 0.96f / 15f, 0.02f, 0.65f);
            tile.GetComponent<MeshRenderer>().sharedMaterial = i % 2 == 0 ? curbMaterial : roadMaterial;
            tile.isStatic = true;
        }
    }

    private void CreateRoadsideDetails()
    {
        for (int i = 12; i < sampleCount; i += 14)
        {
            Vector3 point = Samples[i];
            Vector3 tangent = Tangents[i];
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                post.name = "RoadsidePost";
                post.transform.SetParent(transform, false);
                post.transform.position = point + right * side * (track.roadWidth * 0.5f + 0.7f) + Vector3.up * 0.45f;
                post.transform.localScale = new Vector3(0.12f, 0.9f, 0.12f);
                post.GetComponent<MeshRenderer>().sharedMaterial = curbMaterial;
                post.isStatic = true;
            }
        }
        for (int i = 35; i < sampleCount; i += 55)
        {
            Vector3 point = Samples[i];
            Vector3 tangent = Tangents[i];
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
            GameObject sign = Resources.Load<GameObject>("Environment/Sign") as GameObject;
            if (sign != null)
            {
                GameObject instance = Instantiate(sign, point + right * (track.roadWidth * 0.5f + 2.2f), Quaternion.LookRotation(tangent, Vector3.up));
                instance.transform.SetParent(transform, true);
            }
            if (i % 110 == 35)
            {
                GameObject cart = Resources.Load<GameObject>("Environment/Cart") as GameObject;
                if (cart != null)
                {
                    GameObject instance = Instantiate(cart, point - right * (track.roadWidth * 0.5f + 3.5f), Quaternion.LookRotation(tangent, Vector3.up));
                    instance.transform.SetParent(transform, true);
                }
            }
        }
    }

    private void CreateCheckpoints()
    {
        for (int i = 0; i < checkpointCount; i++)
        {
            int sample = Mathf.RoundToInt(i / (float)checkpointCount * sampleCount) % sampleCount;
            GameObject checkpoint = new GameObject($"Checkpoint_{i:00}", typeof(Checkpoint));
            checkpoint.transform.SetParent(transform, false);
            checkpoint.transform.position = Samples[sample] + Vector3.up * 1.5f;
            checkpoint.GetComponent<Checkpoint>().index = i;
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "Marker";
            marker.transform.SetParent(checkpoint.transform, false);
            marker.transform.localPosition = Vector3.zero;
            marker.transform.localScale = new Vector3(track.roadWidth, 0.18f, 0.18f);
            marker.GetComponent<MeshRenderer>().sharedMaterial = curbMaterial;
            Collider markerCollider = marker.GetComponent<Collider>();
            if (markerCollider != null) markerCollider.enabled = false;
        }
    }

    private void CreateScenery()
    {
        for (int i = 0; i < sampleCount; i += 9)
        {
            Vector3 point = Samples[i];
            Vector3 tangent = Tangents[i];
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 position = point + right * side * (track.roadWidth * 0.5f + 5f + (i % 4));
                position.y = 0f;
                GameObject scenery = Resources.Load<GameObject>($"Environment/{(track.scenery == "forest" ? (i % 2 == 0 ? "Birch" : "Pine") : "House")}") as GameObject;
                if (scenery != null)
                {
                    GameObject instance = Instantiate(scenery, position, Quaternion.Euler(0f, i * 17f, 0f));
                    instance.transform.SetParent(transform, true);
                }
                else
                {
                    CreatePrimitiveTree(position, i);
                }
            }
        }
    }

    private void CreatePrimitiveTree(Vector3 position, int seed)
    {
        GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        trunk.name = "TreeTrunk";
        trunk.transform.SetParent(transform, false);
        trunk.transform.position = position + Vector3.up * 1.8f;
        trunk.transform.localScale = new Vector3(0.35f, 3.6f, 0.35f);
        trunk.GetComponent<MeshRenderer>().sharedMaterial = curbMaterial;
        GameObject crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        crown.name = "TreeCrown";
        crown.transform.SetParent(transform, false);
        crown.transform.position = position + Vector3.up * 4.2f;
        crown.transform.localScale = new Vector3(2.5f, 3.2f, 2.5f);
        crown.GetComponent<MeshRenderer>().sharedMaterial = groundMaterial;
    }

    public Vector3 PointAt(float progress)
    {
        float scaled = Mathf.Repeat(progress, 0.9999f) * sampleCount;
        int index = Mathf.FloorToInt(scaled) % sampleCount;
        int next = (index + 1) % sampleCount;
        return Vector3.Lerp(Samples[index], Samples[next], scaled - Mathf.FloorToInt(scaled));
    }

    public Vector3 TangentAt(float progress)
    {
        int index = Mathf.FloorToInt(Mathf.Repeat(progress, 0.9999f) * sampleCount) % sampleCount;
        return Tangents[index];
    }
}
