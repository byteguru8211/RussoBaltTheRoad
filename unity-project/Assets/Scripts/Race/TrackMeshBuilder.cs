using System.Collections.Generic;
using UnityEngine;

public static class TrackMeshBuilder
{
    public static Mesh Create(string name, List<Vector3> vertices, List<Vector2> uvs, List<int>[] triangles, Material[] materials)
    {
        Mesh mesh = new Mesh { name = name };
        if (vertices.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uvs);
        mesh.subMeshCount = triangles.Length;
        for (int i = 0; i < triangles.Length; i++) mesh.SetTriangles(triangles[i], i);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    public static GameObject CreateObject(string name, Mesh mesh, Material[] materials, Transform parent, bool collider = true, bool shadows = true)
    {
        GameObject gameObject = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
        gameObject.transform.SetParent(parent, false);
        gameObject.GetComponent<MeshFilter>().sharedMesh = mesh;
        MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();
        renderer.sharedMaterials = materials;
        renderer.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = true;
        if (collider) gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;
        gameObject.isStatic = true;
        return gameObject;
    }

    public static void AddQuad(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 d, float u = 0f, float v = 0f, float width = 1f, float height = 1f)
    {
        int index = vertices.Count;
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        vertices.Add(d);
        uvs.Add(new Vector2(u, v));
        uvs.Add(new Vector2(u, v + height));
        uvs.Add(new Vector2(u + width, v + height));
        uvs.Add(new Vector2(u + width, v));
        triangles.AddRange(new[] { index, index + 1, index + 2, index, index + 2, index + 3 });
    }

    public static void AddTriangle(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles, Vector3 a, Vector3 b, Vector3 c, float u = 0f, float v = 0f, float width = 1f, float height = 1f)
    {
        int index = vertices.Count;
        vertices.Add(a);
        vertices.Add(b);
        vertices.Add(c);
        uvs.Add(new Vector2(u, v));
        uvs.Add(new Vector2(u, v + height));
        uvs.Add(new Vector2(u + width, v + height));
        triangles.AddRange(new[] { index, index + 1, index + 2 });
    }

    public static void AddBox(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles, Vector3 center, Quaternion rotation, Vector3 size, float uvScale = 0.25f)
    {
        Vector3 half = size * 0.5f;
        Vector3[] local =
        {
            new Vector3(-half.x, -half.y, -half.z), new Vector3(half.x, -half.y, -half.z),
            new Vector3(half.x, half.y, -half.z), new Vector3(-half.x, half.y, -half.z),
            new Vector3(-half.x, -half.y, half.z), new Vector3(half.x, -half.y, half.z),
            new Vector3(half.x, half.y, half.z), new Vector3(-half.x, half.y, half.z)
        };
        for (int i = 0; i < local.Length; i++) local[i] = center + rotation * local[i];
        AddFace(vertices, uvs, triangles, local, new[] { 0, 3, 2, 1 }, size.z, size.y, uvScale);
        AddFace(vertices, uvs, triangles, local, new[] { 4, 5, 6, 7 }, size.z, size.y, uvScale);
        AddFace(vertices, uvs, triangles, local, new[] { 0, 4, 7, 3 }, size.x, size.y, uvScale);
        AddFace(vertices, uvs, triangles, local, new[] { 1, 2, 6, 5 }, size.x, size.y, uvScale);
        AddFace(vertices, uvs, triangles, local, new[] { 3, 7, 6, 2 }, size.x, size.z, uvScale);
        AddFace(vertices, uvs, triangles, local, new[] { 0, 1, 5, 4 }, size.x, size.z, uvScale);
    }

    public static void AddBeam(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles, Vector3 start, Vector3 end, float width, float height, Vector3 up)
    {
        Vector3 direction = end - start;
        float length = direction.magnitude;
        if (length < 0.001f) return;
        Quaternion rotation = Quaternion.LookRotation(direction / length, up);
        AddBox(vertices, uvs, triangles, (start + end) * 0.5f, rotation, new Vector3(width, height, length));
    }

    public static void AddCylinder(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles, Vector3 center, float radiusBottom, float radiusTop, float height, int segments, Quaternion rotation)
    {
        float yBottom = -height * 0.5f;
        float yTop = height * 0.5f;
        for (int i = 0; i <= segments; i++)
        {
            float angle = i / (float)segments * Mathf.PI * 2f;
            float x = Mathf.Cos(angle);
            float z = Mathf.Sin(angle);
            Vector3 bottom = center + rotation * new Vector3(x * radiusBottom, yBottom, z * radiusBottom);
            Vector3 top = center + rotation * new Vector3(x * radiusTop, yTop, z * radiusTop);
            if (i < segments)
            {
                float u0 = i / (float)segments;
                float u1 = (i + 1) / (float)segments;
                float nextAngle = (i + 1) / (float)segments * Mathf.PI * 2f;
                Vector3 nextBottom = center + rotation * new Vector3(Mathf.Cos(nextAngle) * radiusBottom, yBottom, Mathf.Sin(nextAngle) * radiusBottom);
                Vector3 nextTop = center + rotation * new Vector3(Mathf.Cos(nextAngle) * radiusTop, yTop, Mathf.Sin(nextAngle) * radiusTop);
                AddQuad(vertices, uvs, triangles, bottom, top, nextTop, nextBottom, u0, 0f, u1 - u0, height);
            }
        }
        Vector3 bottomCenter = center + rotation * Vector3.down * height * 0.5f;
        Vector3 topCenter = center + rotation * Vector3.up * height * 0.5f;
        for (int i = 0; i < segments; i++)
        {
            float a0 = i / (float)segments * Mathf.PI * 2f;
            float a1 = (i + 1) / (float)segments * Mathf.PI * 2f;
            Vector3 b0 = center + rotation * new Vector3(Mathf.Cos(a0) * radiusBottom, yBottom, Mathf.Sin(a0) * radiusBottom);
            Vector3 b1 = center + rotation * new Vector3(Mathf.Cos(a1) * radiusBottom, yBottom, Mathf.Sin(a1) * radiusBottom);
            Vector3 t0 = center + rotation * new Vector3(Mathf.Cos(a0) * radiusTop, yTop, Mathf.Sin(a0) * radiusTop);
            Vector3 t1 = center + rotation * new Vector3(Mathf.Cos(a1) * radiusTop, yTop, Mathf.Sin(a1) * radiusTop);
            AddTriangle(vertices, uvs, triangles, bottomCenter, b0, b1);
            AddTriangle(vertices, uvs, triangles, topCenter, t1, t0);
        }
    }

    public static void AddRock(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles, Vector3 center, Vector3 scale, int seed)
    {
        System.Random random = new System.Random(seed);
        Vector3[] ring = new Vector3[7];
        for (int i = 0; i < ring.Length; i++)
        {
            float angle = i / (float)ring.Length * Mathf.PI * 2f;
            float radius = RandomRange(random, 0.75f, 1.2f);
            ring[i] = center + Vector3.Scale(new Vector3(Mathf.Cos(angle) * radius, RandomRange(random, 0.35f, 0.75f), Mathf.Sin(angle) * radius), scale);
        }
        Vector3 top = center + Vector3.up * scale.y * RandomRange(random, 0.7f, 1.1f);
        Vector3 bottom = center - Vector3.up * scale.y * 0.55f;
        for (int i = 0; i < ring.Length; i++)
        {
            int next = (i + 1) % ring.Length;
            AddTriangle(vertices, uvs, triangles, ring[i], top, ring[next]);
            AddTriangle(vertices, uvs, triangles, ring[i], ring[next], bottom);
        }
    }

    public static float RandomRange(System.Random random, float min, float max)
    {
        return min + (float)random.NextDouble() * (max - min);
    }

    private static void AddFace(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles, Vector3[] points, int[] order, float width, float height, float uvScale)
    {
        AddQuad(vertices, uvs, triangles, points[order[0]], points[order[1]], points[order[2]], points[order[3]], 0f, 0f, width * uvScale, height * uvScale);
    }
}
