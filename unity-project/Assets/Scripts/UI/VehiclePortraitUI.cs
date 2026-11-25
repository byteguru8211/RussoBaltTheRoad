using UnityEngine;
using UnityEngine.UI;

public class VehiclePortraitUI : Graphic
{
    [SerializeField] private string vehicleId = "russo_balt_c24_55";
    [SerializeField] private bool showGround = true;
    private float meshScale = 1f;

    public override bool raycastTarget => false;

    protected override void OnRectTransformDimensionsChange()
    {
        base.OnRectTransformDimensionsChange();
        SetVerticesDirty();
    }

    public void SetVehicle(string id)
    {
        vehicleId = id;
        SetVerticesDirty();
    }

    protected override void OnPopulateMesh(VertexHelper helper)
    {
        helper.Clear();
        RectTransform rect = rectTransform;
        float unit = Mathf.Min(rect.rect.width, rect.rect.height);
        if (unit <= 1f) return;
        meshScale = unit;
        VehicleCatalog.VehicleInfo info = VehicleCatalog.Find(vehicleId);
        Color paint = info != null ? info.accent : UITheme.Brass;
        Color dark = new Color(0.025f, 0.04f, 0.033f, 1f);
        Color metal = new Color(0.24f, 0.27f, 0.23f, 1f);
        Color brass = UITheme.Brass;
        Color cream = UITheme.Cream;

        if (showGround)
            AddEllipse(helper, new Vector2(0f, -0.32f), new Vector2(0.48f, 0.045f), new Color(0f, 0f, 0f, 0.38f), 28);

        AddWheel(helper, new Vector2(-0.29f, -0.18f), 0.145f, dark, brass, cream);
        AddWheel(helper, new Vector2(0.29f, -0.18f), 0.145f, dark, brass, cream);
        AddQuad(helper, new Vector2(-0.45f, -0.10f), new Vector2(0.45f, -0.10f), new Vector2(0.45f, 0.015f), new Vector2(-0.45f, 0.015f), dark);
        AddLine(helper, new Vector2(-0.44f, -0.04f), new Vector2(0.44f, -0.04f), 0.014f, brass);

        if (vehicleId == "ford_model_t_1912")
        {
            AddQuad(helper, new Vector2(-0.34f, 0.00f), new Vector2(0.08f, 0.00f), new Vector2(0.08f, 0.31f), new Vector2(-0.34f, 0.31f), paint);
            AddQuad(helper, new Vector2(0.08f, 0.00f), new Vector2(0.40f, 0.00f), new Vector2(0.36f, 0.15f), new Vector2(0.08f, 0.17f), paint);
            AddQuad(helper, new Vector2(-0.27f, 0.10f), new Vector2(-0.04f, 0.10f), new Vector2(-0.04f, 0.25f), new Vector2(-0.27f, 0.25f), dark);
            AddLine(helper, new Vector2(-0.15f, 0.10f), new Vector2(-0.15f, 0.25f), 0.012f, brass);
            AddQuad(helper, new Vector2(-0.35f, 0.30f), new Vector2(0.09f, 0.30f), new Vector2(0.09f, 0.34f), new Vector2(-0.35f, 0.34f), dark);
        }
        else if (vehicleId == "benz_1912_racer_concept")
        {
            AddPolygon(helper, new[] { new Vector2(-0.43f, 0.0f), new Vector2(0.44f, 0.0f), new Vector2(0.48f, 0.12f), new Vector2(0.16f, 0.18f), new Vector2(-0.20f, 0.22f), new Vector2(-0.43f, 0.16f) }, paint);
            AddQuad(helper, new Vector2(-0.17f, 0.13f), new Vector2(0.08f, 0.13f), new Vector2(0.08f, 0.26f), new Vector2(-0.17f, 0.26f), dark);
            AddLine(helper, new Vector2(0.12f, 0.03f), new Vector2(0.42f, 0.03f), 0.012f, brass);
            AddEllipse(helper, new Vector2(-0.15f, 0.25f), new Vector2(0.075f, 0.075f), cream, 16);
        }
        else if (vehicleId == "fiat_s76_1910")
        {
            AddQuad(helper, new Vector2(-0.44f, 0.0f), new Vector2(0.42f, 0.0f), new Vector2(0.42f, 0.25f), new Vector2(-0.44f, 0.25f), paint);
            AddQuad(helper, new Vector2(-0.28f, 0.17f), new Vector2(0.12f, 0.17f), new Vector2(0.12f, 0.35f), new Vector2(-0.28f, 0.35f), paint);
            for (int i = 0; i < 5; i++)
                AddLine(helper, new Vector2(-0.26f + i * 0.085f, 0.18f), new Vector2(-0.26f + i * 0.085f, 0.33f), 0.012f, dark);
            AddCircle(helper, new Vector2(0.27f, 0.16f), 0.065f, brass, 18);
            AddWheel(helper, new Vector2(-0.35f, -0.18f), 0.16f, dark, brass, cream);
            AddWheel(helper, new Vector2(0.35f, -0.18f), 0.16f, dark, brass, cream);
        }
        else if (vehicleId == "mercedes_37_90_1912")
        {
            AddPolygon(helper, new[] { new Vector2(-0.43f, 0.0f), new Vector2(0.43f, 0.0f), new Vector2(0.40f, 0.20f), new Vector2(0.08f, 0.25f), new Vector2(-0.29f, 0.34f), new Vector2(-0.43f, 0.27f) }, paint);
            AddPolygon(helper, new[] { new Vector2(-0.26f, 0.18f), new Vector2(0.07f, 0.18f), new Vector2(0.07f, 0.29f), new Vector2(-0.24f, 0.36f), new Vector2(-0.31f, 0.31f) }, dark);
            AddLine(helper, new Vector2(-0.10f, 0.18f), new Vector2(-0.10f, 0.33f), 0.012f, brass);
            AddLine(helper, new Vector2(-0.40f, 0.02f), new Vector2(0.39f, 0.02f), 0.014f, brass);
        }
        else
        {
            AddQuad(helper, new Vector2(-0.40f, 0.0f), new Vector2(0.40f, 0.0f), new Vector2(0.37f, 0.18f), new Vector2(-0.40f, 0.18f), paint);
            AddQuad(helper, new Vector2(-0.28f, 0.12f), new Vector2(0.05f, 0.12f), new Vector2(0.05f, 0.33f), new Vector2(-0.28f, 0.33f), paint);
            AddQuad(helper, new Vector2(-0.22f, 0.17f), new Vector2(-0.01f, 0.17f), new Vector2(-0.01f, 0.29f), new Vector2(-0.22f, 0.29f), dark);
            AddLine(helper, new Vector2(-0.115f, 0.17f), new Vector2(-0.115f, 0.29f), 0.012f, brass);
            AddQuad(helper, new Vector2(-0.30f, 0.31f), new Vector2(0.06f, 0.31f), new Vector2(0.06f, 0.35f), new Vector2(-0.30f, 0.35f), dark);
            AddLine(helper, new Vector2(0.07f, 0.02f), new Vector2(0.37f, 0.02f), 0.012f, brass);
        }

        AddCircle(helper, new Vector2(0.43f, 0.10f), 0.048f, brass, 16);
        AddCircle(helper, new Vector2(0.43f, 0.10f), 0.034f, cream, 16);
        AddLine(helper, new Vector2(-0.42f, -0.06f), new Vector2(-0.48f, 0.04f), 0.018f, metal);
    }

    private void AddWheel(VertexHelper helper, Vector2 point, float radius, Color tire, Color rim, Color hub)
    {
        AddCircle(helper, point, radius, tire, 24);
        AddCircle(helper, point, radius * 0.68f, rim, 20);
        AddCircle(helper, point, radius * 0.48f, darkColor(), 18);
        for (int i = 0; i < 8; i++)
        {
            float angle = i / 8f * Mathf.PI * 2f;
            AddLine(helper, point, point + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius * 0.58f, 0.009f, rim);
        }
        AddCircle(helper, point, radius * 0.12f, hub, 12);
    }

    private static Color darkColor() => new Color(0.035f, 0.047f, 0.038f, 1f);

    private void AddPolygon(VertexHelper helper, Vector2[] points, Color color)
    {
        for (int i = 1; i < points.Length - 1; i++) AddTriangle(helper, points[0], points[i], points[i + 1], color);
    }

    private void AddQuad(VertexHelper helper, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color color)
    {
        int index = helper.currentVertCount;
        helper.AddVert(Vertex(a, color));
        helper.AddVert(Vertex(b, color));
        helper.AddVert(Vertex(c, color));
        helper.AddVert(Vertex(d, color));
        helper.AddTriangle(index, index + 1, index + 2);
        helper.AddTriangle(index + 2, index + 3, index);
    }

    private void AddTriangle(VertexHelper helper, Vector2 a, Vector2 b, Vector2 c, Color color)
    {
        int index = helper.currentVertCount;
        helper.AddVert(Vertex(a, color));
        helper.AddVert(Vertex(b, color));
        helper.AddVert(Vertex(c, color));
        helper.AddTriangle(index, index + 1, index + 2);
    }

    private void AddLine(VertexHelper helper, Vector2 a, Vector2 b, float width, Color color)
    {
        Vector2 direction = (b - a).normalized;
        Vector2 normal = new Vector2(-direction.y, direction.x) * width * 0.5f;
        AddQuad(helper, a + normal, a - normal, b - normal, b + normal, color);
    }

    private void AddCircle(VertexHelper helper, Vector2 point, float radius, Color color, int segments)
    {
        for (int i = 0; i < segments; i++)
        {
            float a0 = i / (float)segments * Mathf.PI * 2f;
            float a1 = (i + 1) / (float)segments * Mathf.PI * 2f;
            AddTriangle(helper, point, point + new Vector2(Mathf.Cos(a0), Mathf.Sin(a0)) * radius, point + new Vector2(Mathf.Cos(a1), Mathf.Sin(a1)) * radius, color);
        }
    }

    private void AddEllipse(VertexHelper helper, Vector2 point, Vector2 radius, Color color, int segments)
    {
        for (int i = 0; i < segments; i++)
        {
            float a0 = i / (float)segments * Mathf.PI * 2f;
            float a1 = (i + 1) / (float)segments * Mathf.PI * 2f;
            AddTriangle(helper, point, point + new Vector2(Mathf.Cos(a0) * radius.x, Mathf.Sin(a0) * radius.y), point + new Vector2(Mathf.Cos(a1) * radius.x, Mathf.Sin(a1) * radius.y), color);
        }
    }

    private UIVertex Vertex(Vector2 point, Color color)
    {
        UIVertex vertex = new UIVertex { position = point * meshScale, color = color };
        vertex.uv0 = Vector2.zero;
        return vertex;
    }
}
