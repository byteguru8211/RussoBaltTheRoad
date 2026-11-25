using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    private Canvas canvas;
    private Text profileName;
    private Text profileStats;
    private float profileRefreshTimer;

    private void Start()
    {
        if (AchievementSystem.Instance == null)
            new GameObject("AchievementSystem").AddComponent<AchievementSystem>();
        Build();
    }

    private void Build()
    {
        canvas = UIUtils.CreateCanvas("MainMenuCanvas", 20);
        UIUtils.Background(canvas.transform, "UI/Hero", Color.white);
        UIUtils.Scrim(canvas.transform, "LeftScrim", new Color(0.015f, 0.032f, 0.025f, 0.86f), new Vector2(0f, 0f), new Vector2(0.66f, 1f), Vector2.zero, Vector2.zero);
        UIUtils.Scrim(canvas.transform, "BottomScrim", new Color(0.015f, 0.032f, 0.025f, 0.55f), new Vector2(0f, 0f), new Vector2(1f, 0.16f), Vector2.zero, Vector2.zero);
        UIUtils.Panel(canvas.transform, "TopRule", UITheme.Brass, new Vector2(0.07f, 0.93f), new Vector2(0.60f, 0.935f), Vector2.zero, Vector2.zero, false);
        UIUtils.Text(canvas.transform, "Kicker", "СИМУЛЯТОР РАННЕГО АВТОМОБИЛЬНОГО СПОРТА", 20, UITheme.Brass, TextAnchor.MiddleLeft, new Vector2(0.07f, 0.85f), new Vector2(0.62f, 0.91f), Vector2.zero, Vector2.zero);
        UIUtils.Text(canvas.transform, "Title", "RUSSO-BALT\nTHE ROAD", 72, UITheme.Cream, TextAnchor.MiddleLeft, new Vector2(0.07f, 0.62f), new Vector2(0.62f, 0.84f), Vector2.zero, Vector2.zero, UITheme.TitleFont);
        UIUtils.Text(canvas.transform, "Subtitle", "Дорога к Монако · 1912", 26, UITheme.Muted, TextAnchor.MiddleLeft, new Vector2(0.07f, 0.57f), new Vector2(0.60f, 0.62f), Vector2.zero, Vector2.zero);

        UIUtils.Button(canvas.transform, "Expedition", "Выбрать заезд", new Vector2(0.07f, 0.43f), new Vector2(0.36f, 0.52f), Vector2.zero, Vector2.zero, () => RaceSelectionUI.Show(canvas), 30);
        UIUtils.Button(canvas.transform, "Garage", "Гараж", new Vector2(0.07f, 0.32f), new Vector2(0.36f, 0.41f), Vector2.zero, Vector2.zero, () => SceneManager.LoadScene("Garage"), 28);
        UIUtils.Button(canvas.transform, "Network", "Сетевая игра", new Vector2(0.07f, 0.21f), new Vector2(0.36f, 0.30f), Vector2.zero, Vector2.zero, () => NetworkMenuUI.Show(canvas), 28);
        UIUtils.Button(canvas.transform, "Archive", "Архив", new Vector2(0.07f, 0.10f), new Vector2(0.36f, 0.19f), Vector2.zero, Vector2.zero, () => ArchiveUI.Show(canvas), 28);
        UIUtils.Button(canvas.transform, "Settings", "Настройки", new Vector2(0.39f, 0.43f), new Vector2(0.64f, 0.52f), Vector2.zero, Vector2.zero, () => SettingsUI.Show(), 26);
        UIUtils.Button(canvas.transform, "Credits", "Об игре", new Vector2(0.39f, 0.32f), new Vector2(0.64f, 0.41f), Vector2.zero, Vector2.zero, () => CreditsUI.Show(canvas), 26);
        UIUtils.Button(canvas.transform, "Achievements", "Достижения", new Vector2(0.39f, 0.21f), new Vector2(0.64f, 0.30f), Vector2.zero, Vector2.zero, () => AchievementsUI.Show(canvas), 26);
        UIUtils.Button(canvas.transform, "Quit", "Выход", new Vector2(0.39f, 0.10f), new Vector2(0.64f, 0.19f), Vector2.zero, Vector2.zero, Application.Quit, 26);

        ProfileData profile = GameManager.Instance.Profile;
        Image profileCard = UIUtils.Card(canvas.transform, "Profile", UITheme.Dark, new Vector2(0.72f, 0.12f), new Vector2(0.96f, 0.42f), Vector2.zero, Vector2.zero);
        Button profileButton = profileCard.gameObject.AddComponent<Button>();
        profileButton.onClick.AddListener(() => ProfileUI.Show(canvas));
        profileName = UIUtils.Text(canvas.transform, "ProfileName", profile.playerName, 29, UITheme.Brass, TextAnchor.MiddleLeft, new Vector2(0.74f, 0.32f), new Vector2(0.94f, 0.39f), Vector2.zero, Vector2.zero, UITheme.TitleFont);
        profileStats = UIUtils.Text(canvas.transform, "ProfileStats", $"Пройдено: {profile.totalDistanceKm:F1} км\nПобед: {profile.racesWon}\nРубли: {profile.credits:F0}", 22, UITheme.Cream, TextAnchor.UpperLeft, new Vector2(0.74f, 0.14f), new Vector2(0.94f, 0.30f), Vector2.zero, Vector2.zero);
        UIUtils.Text(canvas.transform, "Authors", "Ковалёв Юрий · Валерия Труфанова", 17, UITheme.Muted, TextAnchor.MiddleRight, new Vector2(0.55f, 0.025f), new Vector2(0.96f, 0.07f), Vector2.zero, Vector2.zero);
        UIUtils.Text(canvas.transform, "Version", $"v{Application.version} · RELEASE · LAN", 17, UITheme.Muted, TextAnchor.MiddleLeft, new Vector2(0.07f, 0.025f), new Vector2(0.34f, 0.07f), Vector2.zero, Vector2.zero);
    }

    private void Update()
    {
        profileRefreshTimer -= Time.unscaledDeltaTime;
        if (profileRefreshTimer > 0f || GameManager.Instance == null || profileName == null || profileStats == null)
            return;
        profileRefreshTimer = 0.25f;
        ProfileData profile = GameManager.Instance.Profile;
        profileName.text = profile.playerName;
        profileStats.text = $"Пройдено: {profile.totalDistanceKm:F1} км\nПобед: {profile.racesWon}\nРубли: {profile.credits:F0}";
    }

    private void StartExpedition()
    {
        RaceSelectionUI.Show(canvas);
    }
}
