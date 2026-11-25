using UnityEngine;

public static class UITheme
{
    public static readonly Color Dark = new Color(0.043f, 0.078f, 0.071f, 0.97f);
    public static readonly Color Panel = new Color(0.075f, 0.129f, 0.114f, 0.96f);
    public static readonly Color Raised = new Color(0.114f, 0.180f, 0.153f, 1f);
    public static readonly Color Cream = new Color(0.949f, 0.910f, 0.835f, 1f);
    public static readonly Color Brass = new Color(0.773f, 0.631f, 0.353f, 1f);
    public static readonly Color Muted = new Color(0.655f, 0.702f, 0.659f, 1f);
    public static readonly Color Danger = new Color(0.549f, 0.231f, 0.196f, 1f);
    public static readonly Color Success = new Color(0.435f, 0.545f, 0.463f, 1f);

    public static Font Font => Resources.Load<Font>("Fonts/Arial") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    public static Font TitleFont => Resources.Load<Font>("Fonts/Georgia") ?? Font;
}
