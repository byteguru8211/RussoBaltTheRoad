using UnityEngine;

public class CreditsUI : MonoBehaviour
{
    public static void Show(Canvas canvas)
    {
        RectTransform panel = UIUtils.Panel(canvas.transform, "CreditsPanel", UITheme.Dark, new Vector2(0.20f, 0.12f), new Vector2(0.80f, 0.88f), Vector2.zero, Vector2.zero).rectTransform;
        UIUtils.Text(panel, "Title", "Об игре", 50, UITheme.Cream, TextAnchor.MiddleCenter, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -80), new Vector2(0, -20));
        UIUtils.Text(panel, "Authors", "Авторы игры\nКовалёв Юрий\nВалерия Труфанова", 32, UITheme.Brass, TextAnchor.MiddleCenter, new Vector2(0, 0.40f), new Vector2(1, 0.72f), Vector2.zero, Vector2.zero);
        UIUtils.Text(panel, "Description", "RussoBalt The Road — симулятор раннего автомобильного спорта.\nИсторическая основа: пробег Санкт-Петербург — Монако 1912 года.\nМодели, звуки и материалы подготовлены специально для проекта.", 22, UITheme.Cream, TextAnchor.MiddleCenter, new Vector2(0, 0.12f), new Vector2(1, 0.40f), Vector2.zero, Vector2.zero);
        UIUtils.Button(panel, "Close", "Закрыть", new Vector2(0.35f, 0.03f), new Vector2(0.65f, 0.13f), Vector2.zero, Vector2.zero, () => Destroy(panel.gameObject), 24);
    }
}
