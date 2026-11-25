using UnityEngine;
using UnityEngine.UI;

public class ProfileUI : MonoBehaviour
{
    public static void Show(Canvas canvas)
    {
        GameObject go = new GameObject("ProfileUI", typeof(ProfileUI));
        go.GetComponent<ProfileUI>().Build(canvas);
    }

    private void Build(Canvas canvas)
    {
        UIUtils.Scrim(canvas.transform, "ProfileShade", new Color(0.01f, 0.02f, 0.015f, 0.86f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        RectTransform panel = UIUtils.Card(canvas.transform, "ProfilePanel", UITheme.Dark, new Vector2(0.16f, 0.08f), new Vector2(0.84f, 0.92f), Vector2.zero, Vector2.zero).rectTransform;
        UIUtils.Text(panel, "Title", "Личный кабинет", 50, UITheme.Cream, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(48, -78), new Vector2(-48, -20), UITheme.TitleFont);
        UIUtils.Panel(panel, "Rule", UITheme.Brass, new Vector2(0, 1), new Vector2(1, 1), new Vector2(48, -96), new Vector2(-48, -92), false);

        UIUtils.Card(panel, "Identity", UITheme.Panel, new Vector2(0.03f, 0.58f), new Vector2(0.44f, 0.85f), Vector2.zero, Vector2.zero);
        UIUtils.Text(panel, "Emblem", "РБ", 58, UITheme.Brass, TextAnchor.MiddleCenter, new Vector2(0.06f, 0.66f), new Vector2(0.20f, 0.82f), Vector2.zero, Vector2.zero, UITheme.TitleFont);
        UIUtils.Text(panel, "NameLabel", "Имя гонщика", 20, UITheme.Muted, TextAnchor.MiddleLeft, new Vector2(0.23f, 0.76f), new Vector2(0.41f, 0.81f), Vector2.zero, Vector2.zero);
        UIUtils.InputField(panel, "PlayerName", GameManager.Instance.Profile.playerName, new Vector2(380, -690), v =>
        {
            GameManager.Instance.Profile.playerName = string.IsNullOrWhiteSpace(v) ? "Гонщик" : v;
            GameManager.Instance.Save();
        });
        UIUtils.Text(panel, "Rank", "Пилот экспедиции · уровень I", 22, UITheme.Cream, TextAnchor.MiddleLeft, new Vector2(0.06f, 0.59f), new Vector2(0.41f, 0.64f), Vector2.zero, Vector2.zero);

        ProfileData profile = GameManager.Instance.Profile;
        UIUtils.Card(panel, "Statistics", UITheme.Panel, new Vector2(0.47f, 0.58f), new Vector2(0.97f, 0.85f), Vector2.zero, Vector2.zero);
        UIUtils.Text(panel, "Stats", $"Пройдено     {profile.totalDistanceKm:F1} км\nПобеды          {profile.racesWon}\nСтарты          {profile.racesStarted}\nФиниши         {profile.racesFinished}\nРемонты       {profile.repairs}\nРубли           {profile.credits:F0}", 24, UITheme.Cream, TextAnchor.UpperLeft, new Vector2(0.49f, 0.60f), new Vector2(0.95f, 0.83f), Vector2.zero, Vector2.zero);

        UIUtils.Card(panel, "Records", UITheme.Panel, new Vector2(0.03f, 0.16f), new Vector2(0.44f, 0.53f), Vector2.zero, Vector2.zero);
        string records = "ЛУЧШИЕ ВРЕМЕНА\n";
        foreach (BestTimeRecord record in profile.bestTimes)
            records += $"{record.trackId}   {RaceHUD.FormatTime(record.seconds)}\n";
        if (profile.bestTimes.Count == 0) records += "Пока нет завершённых маршрутов";
        UIUtils.Text(panel, "RecordsText", records, 21, UITheme.Cream, TextAnchor.UpperLeft, new Vector2(0.05f, 0.18f), new Vector2(0.42f, 0.51f), Vector2.zero, Vector2.zero);

        UIUtils.Card(panel, "Achievements", UITheme.Panel, new Vector2(0.47f, 0.16f), new Vector2(0.97f, 0.53f), Vector2.zero, Vector2.zero);
        AchievementSystem system = FindAnyObjectByType<AchievementSystem>();
        int unlocked = system != null ? system.definitions.FindAll(x => profile.achievements.Contains(x.id)).Count : 0;
        int total = system != null ? system.definitions.Count : 7;
        UIUtils.Text(panel, "AchievementProgress", $"ДОСТИЖЕНИЯ   {unlocked} / {total}", 22, UITheme.Brass, TextAnchor.MiddleLeft, new Vector2(0.49f, 0.44f), new Vector2(0.95f, 0.51f), Vector2.zero, Vector2.zero);
        UIUtils.Text(panel, "AchievementHint", "Открывайте маршруты, побеждайте, ремонтируйте машину и исследуйте архив.", 20, UITheme.Muted, TextAnchor.UpperLeft, new Vector2(0.49f, 0.18f), new Vector2(0.95f, 0.42f), Vector2.zero, Vector2.zero);
        UIUtils.Button(panel, "Close", "Закрыть", new Vector2(0.36f, 0.025f), new Vector2(0.64f, 0.10f), Vector2.zero, Vector2.zero, () => Destroy(panel.gameObject), 24);
    }
}
