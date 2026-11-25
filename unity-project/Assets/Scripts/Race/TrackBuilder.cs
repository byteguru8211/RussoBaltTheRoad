using System.Collections.Generic;
using UnityEngine;

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
        CreateGround();
        CreateSampledPath();
        CreateRoadMesh();
        CreateCurbs();
        CreateStartGantry();
        CreateCheckpoints();
        CreateScenery();
    }

    private void CreateMaterials()
    {
        roadMaterial = CreateMaterial("RoadMaterial", track.roadColor, 0.22f);
        groundMaterial = CreateMaterial("GroundMaterial", track.groundColor, 0.05f);
        curbMaterial = CreateMaterial("CurbMaterial", track.curbColor, 0.12f);
    }

    private Material CreateMaterial(string name, Color color, float smoothness)
    {
        Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.name = name;
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Smoothness", smoothness);
        return material;
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
