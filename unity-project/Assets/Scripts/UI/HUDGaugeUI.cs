using UnityEngine;
using UnityEngine.UI;

public class HUDGaugeUI : Graphic
{
    [SerializeField] private float minimum;
    [SerializeField] private float maximum = 180f;
    [SerializeField] private float value;
    [SerializeField] private float startAngle = 140f;
    [SerializeField] private float sweepAngle = 260f;
    [SerializeField] private int tickCount = 9;
    [SerializeField] private Color arcColor = UITheme.Brass;
    [SerializeField] private Color needleColor = UITheme.Cream;

    public override bool raycastTarget => false;

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        SetVerticesDirty();
    }

    public void SetRange(float min, float max)
    {
        minimum = min;
        maximum = Mathf.Max(min + 1f, max);
        SetVerticesDirty();
    }

    public void SetTickCount(int count)
    {
        tickCount = Mathf.Clamp(count, 3, 17);
        SetVerticesDirty();
    }

    public void SetValue(float nextValue)
    {
        float clamped = Mathf.Clamp(nextValue, minimum, maximum);
        if (Mathf.Approximately(clamped, value)) return;
        value = clamped;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper helper)
    {
        helper.Clear();
        RectTransform rect = rectTransform;
        float radius = Mathf.Min(rect.rect.width, rect.rect.height) * 0.46f;
        if (radius <= 1f) return;
        Vector2 origin = Vector2.zero;
        Color track = new Color(0.15f, 0.19f, 0.15f, 0.95f);
        AddArc(helper, origin, radius, radius * 0.94f, startAngle, startAngle + sweepAngle, track, 64);
        AddArc(helper, origin, radius, radius * 0.94f, startAngle, startAngle + sweepAngle, arcColor, 64);

        for (int i = 0; i < tickCount; i++)
        {
            float t = i / (float)Mathf.Max(1, tickCount - 1);
            float angle = Mathf.Deg2Rad * Mathf.Lerp(startAngle, startAngle + sweepAngle, t);
            Vector2 direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float outer = radius * 1.04f;
            float inner = radius * (i % 2 == 0 ? 0.82f : 0.89f);
            AddLine(helper, direction * inner, direction * outer, i % 2 == 0 ? 3f : 2f, i == tickCount - 1 ? UITheme.Danger : UITheme.Cream);
        }

        float normalized = Mathf.InverseLerp(minimum, maximum, value);
        float needleAngle = Mathf.Deg2Rad * Mathf.Lerp(startAngle, startAngle + sweepAngle, normalized);
        Vector2 needleDirection = new Vector2(Mathf.Cos(needleAngle), Mathf.Sin(needleAngle));
        AddLine(helper, -needleDirection * radius * 0.10f, needleDirection * radius * 0.74f, 4f, needleColor);
        AddCircle(helper, origin, radius * 0.07f, UITheme.Brass, 18);
        AddCircle(helper, origin, radius * 0.032f, UITheme.Dark, 14);
    }

    private static void AddArc(VertexHelper helper, Vector2 origin, float outerRadius, float innerRadius, float from, float to, Color color, int segments)
    {
        for (int i = 0; i < segments; i++)
        {
            float a0 = Mathf.Deg2Rad * Mathf.Lerp(from, to, i / (float)segments);
            float a1 = Mathf.Deg2Rad * Mathf.Lerp(from, to, (i + 1) / (float)segments);
            Vector2 d0 = new Vector2(Mathf.Cos(a0), Mathf.Sin(a0));
            Vector2 d1 = new Vector2(Mathf.Cos(a1), Mathf.Sin(a1));
            AddQuad(helper, origin + d0 * innerRadius, origin + d0 * outerRadius, origin + d1 * outerRadius, origin + d1 * innerRadius, color);
        }
    }

    private static void AddQuad(VertexHelper helper, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
    {
        int index = helper.currentVertCount;
        helper.AddVert(Vertex(a, color));
        helper.AddVert(Vertex(b, color));
        helper.AddVert(Vertex(c, color));
        helper.AddVert(Vertex(d, color));
        helper.AddTriangle(index, index + 1, index + 2);
        helper.AddTriangle(index + 2, index + 3, index);
    }

    private static void AddLine(VertexHelper helper, Vector2 a, Vector2 b, float width, Color color)
    {
        Vector2 direction = (b - a).normalized;
        Vector2 normal = new Vector2(-direction.y, direction.x) * width * 0.5f;
        AddQuad(helper, a + normal, a - normal, b - normal, b + normal, color);
    }

    private static void AddCircle(VertexHelper helper, Vector2 point, float radius, Color color, int segments)
    {
        for (int i = 0; i < segments; i++)
        {
            float a0 = i / (float)segments * Mathf.PI * 2f;
            float a1 = (i + 1) / (float)segments * Mathf.PI * 2f;
            int index = helper.currentVertCount;
            helper.AddVert(Vertex(point, color));
            helper.AddVert(Vertex(point + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radius, color));
            helper.AddVert(Vertex(point + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius, color));
            helper.AddTriangle(index, index + 1, index + 2);
        }
    }

    private static UIVertex Vertex(Vector2 point, Color color)
    {
        UIVertex vertex = new UIVertex { position = point, color = color };
        vertex.uv0 = Vector2.zero;
        return vertex;
    }
}
