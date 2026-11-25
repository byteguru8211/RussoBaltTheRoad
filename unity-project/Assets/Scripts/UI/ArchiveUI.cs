using UnityEngine;

public class ArchiveUI : MonoBehaviour
{
    public static void Show(Canvas canvas)
    {
        RectTransform panel = UIUtils.Panel(canvas.transform, "ArchivePanel", UITheme.Dark, new Vector2(0.16f, 0.07f), new Vector2(0.84f, 0.93f), Vector2.zero, Vector2.zero).rectTransform;
        UIUtils.Text(panel, "Title", "Архив экспедиции", 46, UITheme.Cream, TextAnchor.MiddleCenter, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -75), new Vector2(0, -20));
        string text = "1912 год. Андрей Нагель и Вадим Михайлов проезжают из Санкт-Петербурга в Монако на Russo-Balt C24/55.\n\nМашина приходит первой физически, но по регламенту занимает девятое место. Главное — не только скорость, но и выносливость, ремонт, навигация и холод.\n\nВ игре исторические факты отделены от вымышленных событий. Оригинальный C24/55 не сохранился, поэтому модель — реконструкция.";
        UIUtils.Text(panel, "Text", text, 24, UITheme.Cream, TextAnchor.UpperLeft, new Vector2(0, 0), new Vector2(1, 0.86f), new Vector2(45, 20), new Vector2(-45, -20));
        UIUtils.Button(panel, "Achievements", "Достижения", new Vector2(0.08f, 0.03f), new Vector2(0.30f, 0.12f), Vector2.zero, Vector2.zero, () => { Destroy(panel.gameObject); AchievementsUI.Show(canvas); }, 22);
        UIUtils.Button(panel, "Close", "Закрыть", new Vector2(0.70f, 0.03f), new Vector2(0.92f, 0.12f), Vector2.zero, Vector2.zero, () => Destroy(panel.gameObject), 22);
    }
}
