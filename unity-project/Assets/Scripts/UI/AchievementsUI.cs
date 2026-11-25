using UnityEngine;

public class AchievementsUI : MonoBehaviour
{
    public static void Show(Canvas canvas)
    {
        RectTransform panel = UIUtils.Panel(canvas.transform, "AchievementsPanel", UITheme.Dark, new Vector2(0.18f, 0.08f), new Vector2(0.82f, 0.92f), Vector2.zero, Vector2.zero).rectTransform;
        UIUtils.Text(panel, "Title", "Достижения", 48, UITheme.Cream, TextAnchor.MiddleCenter, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -75), new Vector2(0, -20));
        AchievementSystem system = FindAnyObjectByType<AchievementSystem>();
        if (system == null)
            return;
        float y = -120f;
        foreach (AchievementDefinition definition in system.definitions)
        {
            bool unlocked = GameManager.Instance.Profile.achievements.Contains(definition.id);
            UIUtils.Text(panel, definition.id, (unlocked ? "◆ " : "◇ ") + definition.title, 24, unlocked ? UITheme.Brass : UITheme.Muted, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(35, y - 32), new Vector2(-35, y));
            UIUtils.Text(panel, definition.id + "Desc", definition.description, 18, unlocked ? UITheme.Cream : UITheme.Muted, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(65, y - 58), new Vector2(-35, y - 34));
            y -= 76f;
        }
        UIUtils.Button(panel, "Close", "Закрыть", new Vector2(0.35f, 0.02f), new Vector2(0.65f, 0.11f), Vector2.zero, Vector2.zero, () => Destroy(panel.gameObject), 24);
    }
}
