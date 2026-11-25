using UnityEngine;

public static class UITheme
{
    public static readonly Color Dark = new Color(0.035f, 0.075f, 0.060f, 0.96f);
    public static readonly Color Panel = new Color(0.055f, 0.105f, 0.085f, 0.94f);
    public static readonly Color Cream = new Color(0.91f, 0.87f, 0.76f);
    public static readonly Color Brass = new Color(0.72f, 0.55f, 0.28f);
    public static readonly Color Muted = new Color(0.62f, 0.64f, 0.57f);
    public static readonly Color Danger = new Color(0.65f, 0.20f, 0.13f);
    public static readonly Color Success = new Color(0.28f, 0.55f, 0.34f);

    public static Font Font => Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
}
