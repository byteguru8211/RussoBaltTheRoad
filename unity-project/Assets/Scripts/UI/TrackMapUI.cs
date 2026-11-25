using UnityEngine;
using UnityEngine.UI;

public class TrackMapUI : Graphic
{
    [SerializeField] private Vector3[] points;
    [SerializeField] private Color routeColor = UITheme.Brass;
    [SerializeField] private Color checkpointColor = UITheme.Cream;

    private Bounds bounds;
    private float scale = 1f;
    private Vector2 center;

    public override bool raycastTarget => false;

    public void SetWaypoints(Vector3[] waypoints)
    {
        points = waypoints != null && waypoints.Length > 1 ? (Vector3[])waypoints.Clone() : null;
        CalculateBounds();
        SetVerticesDirty();
    }

    private void CalculateBounds()
    {
        if (points == null || points.Length == 0)
        {
            bounds = new Bounds(Vector3.zero, Vector3.one);
            scale = 1f;
            center = Vector2.zero;
            return;
        }
        float minX = float.MaxValue, maxX = float.MinValue;
        float minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (Vector3 point in points)
        {
            minX = Mathf.Min(minX, point.x);
            maxX = Mathf.Max(maxX, point.x);
            minZ = Mathf.Min(minZ, point.z);
            maxZ = Mathf.Max(maxZ, point.z);
        }
        center = new Vector2((minX + maxX) * 0.5f, (minZ + maxZ) * 0.5f);
        bounds.center = new Vector3(center.x, 0f, center.y);
        bounds.size = new Vector3(Mathf.Max(1f, maxX - minX), 1f, Mathf.Max(1f, maxZ - minZ));
        RectTransform rect = rectTransform;
        float padding = Mathf.Min(rect.rect.width, rect.rect.height) * 0.12f;
        float usableWidth = Mathf.Max(1f, rect.rect.width - padding * 2f);
        float usableHeight = Mathf.Max(1f, rect.rect.height - padding * 2f);
        scale = Mathf.Min(usableWidth / bounds.size.x, usableHeight / bounds.size.z);
    }

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        CalculateBounds();
        SetVerticesDirty();
    }

    public Vector2 WorldToMapPoint(Vector3 world)
    {
        CalculateBounds();
        Vector2 point = new Vector2((world.x - center.x) * scale, (world.z - center.y) * scale);
        Vector2 half = rectTransform.rect.size * 0.5f;
        float inset = Mathf.Min(10f, Mathf.Min(half.x, half.y) * 0.25f);
        point.x = Mathf.Clamp(point.x, -half.x + inset, half.x - inset);
        point.y = Mathf.Clamp(point.y, -half.y + inset, half.y - inset);
        return point;
    }

    private Vector2 MapPoint(Vector3 point) => new Vector2((point.x - center.x) * scale, (point.z - center.y) * scale);

    protected override void OnPopulateMesh(VertexHelper helper)
    {
        helper.Clear();
        if (points == null || points.Length < 2) return;
        CalculateBounds();
        Color edge = new Color(0.02f, 0.04f, 0.032f, 0.95f);
        for (int i = 0; i < points.Length; i++)
            AddLine(helper, MapPoint(points[i]), MapPoint(points[(i + 1) % points.Length]), 8f, edge);
        for (int i = 0; i < points.Length; i++)
            AddLine(helper, MapPoint(points[i]), MapPoint(points[(i + 1) % points.Length]), 3.5f, routeColor);
        foreach (Vector3 point in points)
            AddCircle(helper, MapPoint(point), 2.5f, checkpointColor, 8);
        Vector2 start = MapPoint(points[0]);
        AddQuad(helper, start + new Vector2(-5f, -5f), start + new Vector2(5f, -5f), start + new Vector2(5f, 5f), start + new Vector2(-5f, 5f), UITheme.Cream);
        AddQuad(helper, start + new Vector2(-5f, 0f), start + new Vector2(0f, 0f), start + new Vector2(0f, 5f), start + new Vector2(-5f, 5f), UITheme.Dark);
        AddQuad(helper, start + new Vector2(0f, -5f), start + new Vector2(5f, -5f), start + new Vector2(5f, 0f), start + new Vector2(0f, 0f), UITheme.Dark);
    }

    private static void AddLine(VertexHelper helper, Vector2 a, Vector2 b, float width, Color color)
    {
        Vector2 direction = (b - a).normalized;
        Vector2 normal = new Vector2(-direction.y, direction.x) * width * 0.5f;
        AddQuad(helper, a + normal, a - normal, b - normal, b + normal, color);
    }

    private static void AddQuad(VertexHelper helper, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
    {
        UIVertex v0 = Vertex(a, color);
        UIVertex v1 = Vertex(b, color);
        UIVertex v2 = Vertex(c, color);
        UIVertex v3 = Vertex(d, color);
        helper.AddVert(v0);
        helper.AddVert(v1);
        helper.AddVert(v2);
        helper.AddVert(v3);
        int index = helper.currentVertCount - 4;
        helper.AddTriangle(index, index + 1, index + 2);
        helper.AddTriangle(index + 2, index + 3, index);
    }

    private static void AddCircle(VertexHelper helper, Vector2 point, float radius, Color color, int segments)
    {
        for (int i = 0; i < segments; i++)
        {
            float a0 = i / (float)segments * Mathf.PI * 2f;
            float a1 = (i + 1) / (float)segments * Mathf.PI * 2f;
            AddTriangle(helper, point, point + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radius, point + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius, color);
        }
    }

    private static void AddTriangle(VertexHelper helper, Vector2 a, Vector2 b, Vector2 c, Color color)
    {
        int index = helper.currentVertCount;
        helper.AddVert(Vertex(a, color));
        helper.AddVert(Vertex(b, color));
        helper.AddVert(Vertex(c, color));
        helper.AddTriangle(index, index + 1, index + 2);
    }

    private static UIVertex Vertex(Vector2 point, Color color)
    {
        UIVertex vertex = new UIVertex { position = point, color = color };
        vertex.uv0 = Vector2.zero;
        return vertex;
    }
}
