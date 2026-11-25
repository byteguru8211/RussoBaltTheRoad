using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ProfileUI : MonoBehaviour
{
    private GameObject modalRoot;
    private ProfileData profile;
    private InputField nameField;

    public static void Show(Canvas canvas)
    {
        if (canvas == null)
        {
            Debug.LogWarning("ProfileUI requires a canvas.");
            return;
        }
        if (FindAnyObjectByType<ProfileUI>() != null)
            return;

        GameObject go = new GameObject("ProfileModal", typeof(RectTransform), typeof(ProfileUI));
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.SetParent(canvas.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        go.transform.SetAsLastSibling();
        ProfileUI ui = go.GetComponent<ProfileUI>();
        ui.modalRoot = go;
        ui.Build();
    }

    private void Build()
    {
        if (GameManager.Instance == null || GameManager.Instance.Profile == null)
        {
            Debug.LogError("ProfileUI cannot open without GameManager.Profile.");
            Close();
            return;
        }

        profile = GameManager.Instance.Profile;
        Transform parent = modalRoot.transform;
        UIUtils.Scrim(parent, "ProfileShade", new Color(0.01f, 0.02f, 0.015f, 0.86f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        GameObject shade = parent.Find("ProfileShade").gameObject;
        shade.GetComponent<Image>().raycastTarget = true;

        RectTransform panel = UIUtils.Card(parent, "ProfilePanel", UITheme.Dark, new Vector2(0.16f, 0.08f), new Vector2(0.84f, 0.92f), Vector2.zero, Vector2.zero).rectTransform;
        UIUtils.Text(panel, "Title", "Личный кабинет", 50, UITheme.Cream, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(48, -78), new Vector2(-48, -20), UITheme.TitleFont);
        UIUtils.Panel(panel, "Rule", UITheme.Brass, new Vector2(0, 1), new Vector2(1, 1), new Vector2(48, -96), new Vector2(-48, -92), false);

        UIUtils.Card(panel, "Identity", UITheme.Panel, new Vector2(0.03f, 0.58f), new Vector2(0.44f, 0.85f), Vector2.zero, Vector2.zero);
        UIUtils.Text(panel, "Emblem", "РБ", 58, UITheme.Brass, TextAnchor.MiddleCenter, new Vector2(0.06f, 0.66f), new Vector2(0.20f, 0.82f), Vector2.zero, Vector2.zero, UITheme.TitleFont);
        UIUtils.Text(panel, "NameLabel", "Имя гонщика", 20, UITheme.Muted, TextAnchor.MiddleLeft, new Vector2(0.23f, 0.765f), new Vector2(0.41f, 0.82f), Vector2.zero, Vector2.zero);
        nameField = UIUtils.InputField(panel, "PlayerName", SaveSystem.NormalizePlayerName(profile.playerName), Vector2.zero, _ => CommitName());
        RectTransform nameRect = (RectTransform)nameField.transform;
        nameRect.anchorMin = new Vector2(0.23f, 0.685f);
        nameRect.anchorMax = new Vector2(0.41f, 0.75f);
        nameRect.offsetMin = Vector2.zero;
        nameRect.offsetMax = Vector2.zero;
        nameField.characterLimit = 32;
        UIUtils.Text(panel, "Rank", "Пилот экспедиции · уровень I", 22, UITheme.Cream, TextAnchor.MiddleLeft, new Vector2(0.06f, 0.59f), new Vector2(0.41f, 0.64f), Vector2.zero, Vector2.zero);

        UIUtils.Card(panel, "Statistics", UITheme.Panel, new Vector2(0.47f, 0.58f), new Vector2(0.97f, 0.85f), Vector2.zero, Vector2.zero);
        UIUtils.Text(panel, "Stats", $"Пройдено     {profile.totalDistanceKm:F1} км\nПобеды          {profile.racesWon}\nСтарты          {profile.racesStarted}\nФиниши         {profile.racesFinished}\nРемонты       {profile.repairs}\nРубли           {profile.credits:F0}", 24, UITheme.Cream, TextAnchor.UpperLeft, new Vector2(0.49f, 0.60f), new Vector2(0.95f, 0.83f), Vector2.zero, Vector2.zero);

        UIUtils.Card(panel, "Records", UITheme.Panel, new Vector2(0.03f, 0.16f), new Vector2(0.44f, 0.53f), Vector2.zero, Vector2.zero);
        string records = "ЛУЧШИЕ ВРЕМЕНА\n";
        if (profile.bestTimes != null)
        {
            foreach (BestTimeRecord record in profile.bestTimes)
            {
                if (record != null)
                {
                    TrackData track = GameManager.LoadTrack(record.trackId);
                    string trackName = track != null ? track.displayName : record.trackId;
                    records += $"{trackName}   {RaceHUD.FormatTime(record.seconds)}\n";
                }
            }
        }
        if (profile.bestTimes == null || profile.bestTimes.Count == 0)
            records += "Пока нет завершённых маршрутов";
        UIUtils.Text(panel, "RecordsText", records, 21, UITheme.Cream, TextAnchor.UpperLeft, new Vector2(0.05f, 0.18f), new Vector2(0.42f, 0.51f), Vector2.zero, Vector2.zero);

        UIUtils.Card(panel, "Achievements", UITheme.Panel, new Vector2(0.47f, 0.16f), new Vector2(0.97f, 0.53f), Vector2.zero, Vector2.zero);
        AchievementSystem system = FindAnyObjectByType<AchievementSystem>();
        int unlocked = system != null ? system.definitions.FindAll(x => profile.achievements != null && profile.achievements.Contains(x.id)).Count : 0;
        int total = system != null ? system.definitions.Count : 7;
        UIUtils.Text(panel, "AchievementProgress", $"ДОСТИЖЕНИЯ   {unlocked} / {total}", 22, UITheme.Brass, TextAnchor.MiddleLeft, new Vector2(0.49f, 0.44f), new Vector2(0.95f, 0.51f), Vector2.zero, Vector2.zero);
        UIUtils.Text(panel, "AchievementHint", "Открывайте маршруты, побеждайте, ремонтируйте машину и исследуйте архив.", 20, UITheme.Muted, TextAnchor.UpperLeft, new Vector2(0.49f, 0.18f), new Vector2(0.95f, 0.42f), Vector2.zero, Vector2.zero);
        UIUtils.Button(panel, "Close", "Закрыть", new Vector2(0.36f, 0.025f), new Vector2(0.64f, 0.10f), Vector2.zero, Vector2.zero, Close, 24);
    }

    private void CommitName()
    {
        if (profile == null)
            return;
        string normalized = SaveSystem.NormalizePlayerName(nameField != null ? nameField.text : profile.playerName);
        profile.playerName = normalized;
        if (nameField != null && nameField.text != normalized)
            nameField.text = normalized;
        GameManager.Instance.Save();

        GameObject profileName = GameObject.Find("ProfileName");
        if (profileName != null)
        {
            Text label = profileName.GetComponent<Text>();
            if (label != null)
                label.text = normalized;
        }
    }

    public void Close()
    {
        CommitName();
        if (modalRoot != null)
            Destroy(modalRoot);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        Gamepad pad = Gamepad.current;
        if ((keyboard != null && keyboard.escapeKey.wasPressedThisFrame) || (pad != null && pad.buttonEast.wasPressedThisFrame))
            Close();
    }
}
