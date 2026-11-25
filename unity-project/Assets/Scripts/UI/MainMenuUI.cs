using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuUI : MonoBehaviour
{
    private Canvas canvas;

    private void Start()
    {
        if (AchievementSystem.Instance == null)
            new GameObject("AchievementSystem").AddComponent<AchievementSystem>();
        Build();
    }

    private void Build()
    {
        canvas = UIUtils.CreateCanvas("MainMenuCanvas", 10);
        UIUtils.Background(canvas.transform, "UI/Hero", new Color(0.30f, 0.35f, 0.31f, 1f));
        UIUtils.Panel(canvas.transform, "Shade", new Color(0.01f, 0.025f, 0.018f, 0.64f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        UIUtils.Text(canvas.transform, "Kicker", "СИМУЛЯТОР РАННЕГО АВТОМОБИЛЬНОГО СПОРТА", 19, UITheme.Brass, TextAnchor.MiddleLeft, new Vector2(0.07f, 0.80f), new Vector2(0.70f, 0.86f), Vector2.zero, Vector2.zero);
        UIUtils.Text(canvas.transform, "Title", "RUSSO-BALT\nTHE ROAD", 66, UITheme.Cream, TextAnchor.MiddleLeft, new Vector2(0.07f, 0.57f), new Vector2(0.70f, 0.81f), Vector2.zero, Vector2.zero);
        UIUtils.Text(canvas.transform, "Subtitle", "Дорога к Монако · 1912", 25, UITheme.Muted, TextAnchor.MiddleLeft, new Vector2(0.07f, 0.53f), new Vector2(0.60f, 0.58f), Vector2.zero, Vector2.zero);

        UIUtils.Button(canvas.transform, "Expedition", "Экспедиция", new Vector2(0.07f, 0.37f), new Vector2(0.36f, 0.46f), Vector2.zero, Vector2.zero, StartExpedition, 28);
        UIUtils.Button(canvas.transform, "Garage", "Гараж", new Vector2(0.07f, 0.27f), new Vector2(0.36f, 0.36f), Vector2.zero, Vector2.zero, () => SceneManager.LoadScene("Garage"), 28);
        UIUtils.Button(canvas.transform, "Archive", "Архив", new Vector2(0.07f, 0.17f), new Vector2(0.36f, 0.26f), Vector2.zero, Vector2.zero, () => ArchiveUI.Show(canvas), 28);
        UIUtils.Button(canvas.transform, "Settings", "Настройки", new Vector2(0.39f, 0.37f), new Vector2(0.64f, 0.46f), Vector2.zero, Vector2.zero, () => SettingsUI.Show(), 26);
        UIUtils.Button(canvas.transform, "Credits", "Об игре", new Vector2(0.39f, 0.27f), new Vector2(0.64f, 0.36f), Vector2.zero, Vector2.zero, () => CreditsUI.Show(canvas), 26);
        UIUtils.Button(canvas.transform, "Quit", "Выход", new Vector2(0.39f, 0.17f), new Vector2(0.64f, 0.26f), Vector2.zero, Vector2.zero, Application.Quit, 26);

        ProfileData profile = GameManager.Instance.Profile;
        UIUtils.Panel(canvas.transform, "Profile", UITheme.Dark, new Vector2(0.72f, 0.08f), new Vector2(0.96f, 0.40f), Vector2.zero, Vector2.zero);
        UIUtils.Text(canvas.transform, "ProfileName", profile.playerName, 27, UITheme.Brass, TextAnchor.MiddleLeft, new Vector2(0.74f, 0.31f), new Vector2(0.94f, 0.38f), Vector2.zero, Vector2.zero);
        UIUtils.Text(canvas.transform, "ProfileStats", $"Пройдено: {profile.totalDistanceKm:F1} км\nПобед: {profile.racesWon}\nРубли: {profile.credits:F0}", 21, UITheme.Cream, TextAnchor.UpperLeft, new Vector2(0.74f, 0.10f), new Vector2(0.94f, 0.30f), Vector2.zero, Vector2.zero);
        UIUtils.Text(canvas.transform, "Authors", "Ковалёв Юрий · Валерия Труфанова", 16, UITheme.Muted, TextAnchor.MiddleRight, new Vector2(0.55f, 0.02f), new Vector2(0.96f, 0.06f), Vector2.zero, Vector2.zero);
    }

    private void StartExpedition()
    {
        string trackId = GameManager.Instance.SelectedTrackId;
        string scene = trackId == "alps_pass" ? "Race_Alps" : trackId == "monaco_finish" ? "Race_Monaco" : "Race_Winter";
        SceneManager.LoadScene(scene);
    }
}
