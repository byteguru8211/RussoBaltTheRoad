using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class RaceSelectionUI : MonoBehaviour
{
    public static RaceSelectionUI Instance { get; private set; }

    private Canvas parentCanvas;
    private RectTransform root;
    private string selectedTrackId;
    private string inspectedTrackId;
    private RaceType selectedRaceType;
    private Text detailTitle;
    private Text detailChapter;
    private Text detailDescription;
    private Text detailSpecs;
    private Text detailRequirement;
    private Text statusText;
    private Button startButton;
    private readonly Dictionary<string, Button> trackButtons = new Dictionary<string, Button>();
    private readonly Dictionary<string, Text> trackStatusLabels = new Dictionary<string, Text>();
    private readonly Dictionary<RaceType, Button> modeButtons = new Dictionary<RaceType, Button>();

    public static void Show(Canvas parent)
    {
        if (Instance != null)
        {
            Instance.Close();
            return;
        }
        GameObject go = new GameObject("RaceSelectionUI", typeof(RaceSelectionUI));
        RaceSelectionUI ui = go.GetComponent<RaceSelectionUI>();
        ui.parentCanvas = parent;
        ui.selectedTrackId = GameManager.Instance.SelectedTrackId;
        ui.inspectedTrackId = ui.selectedTrackId;
        ui.selectedRaceType = GameManager.Instance.SelectedRaceType;
        Instance = ui;
        ui.Build();
    }

    private void Build()
    {
        root = UIUtils.Rect(parentCanvas.transform, "RaceSelection", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        UIUtils.Background(root, "UI/Hero", new Color(0.22f, 0.27f, 0.23f, 1f));
        UIUtils.Panel(root, "Shade", new Color(0.008f, 0.020f, 0.016f, 0.95f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        UIUtils.Panel(root, "TopRule", UITheme.Brass, new Vector2(0.04f, 0.947f), new Vector2(0.96f, 0.950f), Vector2.zero, Vector2.zero, false);
        UIUtils.Text(root, "Kicker", "RUSSO-BALT  /  RACE CONTROL  /  1912", 18, UITheme.Brass, TextAnchor.MiddleLeft, new Vector2(0.04f, 0.895f), new Vector2(0.62f, 0.935f), Vector2.zero, Vector2.zero);
        UIUtils.Text(root, "Title", "ВЫБОР ЗАЕЗДА", 54, UITheme.Cream, TextAnchor.MiddleLeft, new Vector2(0.04f, 0.825f), new Vector2(0.62f, 0.895f), Vector2.zero, Vector2.zero, UITheme.TitleFont);
        UIUtils.Text(root, "Progress", $"ПРОЙДЕНО  {GameManager.Instance.Profile.racesFinished}   ·   РУБЛИ  {GameManager.Instance.Profile.credits:F0}", 19, UITheme.Muted, TextAnchor.MiddleRight, new Vector2(0.64f, 0.865f), new Vector2(0.96f, 0.92f), Vector2.zero, Vector2.zero);

        UIUtils.Text(root, "TracksLabel", "МАРШРУТ ЭКСПЕДИЦИИ", 19, UITheme.Brass, TextAnchor.MiddleLeft, new Vector2(0.04f, 0.775f), new Vector2(0.64f, 0.815f), Vector2.zero, Vector2.zero);
        List<TrackData> tracks = GameManager.GetTracks();
        for (int i = 0; i < tracks.Count; i++)
        {
            int column = i % 2;
            int row = i / 2;
            float top = 0.765f - row * 0.135f;
            AddTrackCard(tracks[i], new Vector2(0.04f + column * 0.305f, top - 0.12f), new Vector2(0.325f + column * 0.305f, top));
        }

        UIUtils.Card(root, "Details", UITheme.Dark, new Vector2(0.68f, 0.355f), new Vector2(0.96f, 0.80f), Vector2.zero, Vector2.zero);
        UIUtils.Text(root, "DetailKicker", "ПАСПОРТ МАРШРУТА", 17, UITheme.Brass, TextAnchor.MiddleLeft, new Vector2(0.705f, 0.745f), new Vector2(0.935f, 0.785f), Vector2.zero, Vector2.zero);
        detailTitle = UIUtils.Text(root, "DetailTitle", "", 31, UITheme.Cream, TextAnchor.MiddleLeft, new Vector2(0.705f, 0.665f), new Vector2(0.935f, 0.745f), Vector2.zero, Vector2.zero, UITheme.TitleFont);
        detailChapter = UIUtils.Text(root, "DetailChapter", "", 17, UITheme.Muted, TextAnchor.MiddleLeft, new Vector2(0.705f, 0.625f), new Vector2(0.935f, 0.665f), Vector2.zero, Vector2.zero);
        detailDescription = UIUtils.Text(root, "DetailDescription", "", 18, UITheme.Cream, TextAnchor.UpperLeft, new Vector2(0.705f, 0.505f), new Vector2(0.935f, 0.62f), Vector2.zero, Vector2.zero);
        detailSpecs = UIUtils.Text(root, "DetailSpecs", "", 18, UITheme.Brass, TextAnchor.UpperLeft, new Vector2(0.705f, 0.405f), new Vector2(0.935f, 0.50f), Vector2.zero, Vector2.zero);
        detailRequirement = UIUtils.Text(root, "DetailRequirement", "", 17, UITheme.Muted, TextAnchor.MiddleLeft, new Vector2(0.705f, 0.365f), new Vector2(0.935f, 0.40f), Vector2.zero, Vector2.zero);

        UIUtils.Text(root, "ModesLabel", "ФОРМАТ ЗАЕЗДА", 19, UITheme.Brass, TextAnchor.MiddleLeft, new Vector2(0.04f, 0.305f), new Vector2(0.64f, 0.345f), Vector2.zero, Vector2.zero);
        AddModeButton(RaceType.Circuit, "КРУГ", new Vector2(0.04f, 0.225f), new Vector2(0.235f, 0.295f));
        AddModeButton(RaceType.Sprint, "СПРИНТ", new Vector2(0.252f, 0.225f), new Vector2(0.447f, 0.295f));
        AddModeButton(RaceType.Checkpoint, "ЧЕКПОИНТЫ", new Vector2(0.464f, 0.225f), new Vector2(0.64f, 0.295f));
        UIUtils.Text(root, "ModeDescription", RaceTypeInfo.Description(selectedRaceType), 17, UITheme.Muted, TextAnchor.MiddleLeft, new Vector2(0.04f, 0.185f), new Vector2(0.64f, 0.22f), Vector2.zero, Vector2.zero);

        UIUtils.Button(root, "Back", "←  МЕНЮ", new Vector2(0.04f, 0.075f), new Vector2(0.20f, 0.155f), Vector2.zero, Vector2.zero, Close, 23);
        UIUtils.Button(root, "Garage", "ГАРАЖ", new Vector2(0.222f, 0.075f), new Vector2(0.40f, 0.155f), Vector2.zero, Vector2.zero, () => SceneManager.LoadScene("Garage"), 23);
        UIUtils.Button(root, "Network", "LAN / СЕТЬ", new Vector2(0.422f, 0.075f), new Vector2(0.64f, 0.155f), Vector2.zero, Vector2.zero, () =>
        {
            Close();
            NetworkMenuUI.Show(parentCanvas);
        }, 23);
        startButton = UIUtils.Button(root, "Start", "НА СТАРТ  →", new Vector2(0.68f, 0.235f), new Vector2(0.96f, 0.325f), Vector2.zero, Vector2.zero, StartRace, 29, UITheme.TitleFont);
        statusText = UIUtils.Text(root, "Status", "", 19, UITheme.Brass, TextAnchor.MiddleLeft, new Vector2(0.68f, 0.165f), new Vector2(0.96f, 0.215f), Vector2.zero, Vector2.zero);
        UIUtils.Text(root, "Hint", "ENTER — старт   ·   ESC — назад", 16, UITheme.Muted, TextAnchor.MiddleRight, new Vector2(0.68f, 0.085f), new Vector2(0.96f, 0.135f), Vector2.zero, Vector2.zero);
        Refresh();
    }

    private void AddTrackCard(TrackData track, Vector2 min, Vector2 max)
    {
        Button button = UIUtils.Button(root, "Track_" + track.id, "", min, max, Vector2.zero, Vector2.zero, () => InspectTrack(track.id), 24);
        RectTransform mapRect = UIUtils.Rect(button.transform, "Map", new Vector2(0.015f, 0.10f), new Vector2(0.16f, 0.90f), Vector2.zero, Vector2.zero);
        TrackMapUI map = mapRect.gameObject.AddComponent<TrackMapUI>();
        map.SetWaypoints(track.waypoints);
        UIUtils.Text(button.transform, "Name", track.displayName, 19, UITheme.Cream, TextAnchor.MiddleLeft, new Vector2(0.18f, 0.46f), new Vector2(0.76f, 0.94f), Vector2.zero, Vector2.zero, UITheme.TitleFont);
        UIUtils.Text(button.transform, "Chapter", $"{track.chapter}   ·   {track.Location}", 12, UITheme.Muted, TextAnchor.MiddleLeft, new Vector2(0.18f, 0.08f), new Vector2(0.98f, 0.43f), Vector2.zero, Vector2.zero);
        Text state = UIUtils.Text(button.transform, "State", "", 11, UITheme.Brass, TextAnchor.MiddleRight, new Vector2(0.62f, 0.46f), new Vector2(0.97f, 0.92f), Vector2.zero, Vector2.zero);
        trackButtons[track.id] = button;
        trackStatusLabels[track.id] = state;
    }

    private void AddModeButton(RaceType type, string label, Vector2 min, Vector2 max)
    {
        Button button = UIUtils.Button(root, "Mode_" + type, label, min, max, Vector2.zero, Vector2.zero, () =>
        {
            selectedRaceType = type;
            GameManager.Instance.SelectedRaceType = type;
            Refresh();
        }, 22);
        modeButtons[type] = button;
    }

    private void InspectTrack(string trackId)
    {
        inspectedTrackId = trackId;
        if (GameManager.Instance.IsTrackUnlocked(trackId))
        {
            selectedTrackId = trackId;
            GameManager.Instance.SelectedTrackId = trackId;
            statusText.color = UITheme.Muted;
            statusText.text = "";
        }
        else
        {
            statusText.color = UITheme.Danger;
            statusText.text = GameManager.Instance.GetTrackUnlockRequirement(trackId);
        }
        Refresh();
    }

    private void Refresh()
    {
        TrackData inspected = GameManager.LoadTrack(inspectedTrackId);
        foreach (var pair in trackButtons)
        {
            bool unlocked = GameManager.Instance.IsTrackUnlocked(pair.Key);
            bool active = pair.Key == inspectedTrackId;
            pair.Value.image.color = active ? UITheme.Raised : UITheme.Panel;
            pair.Value.transform.Find("Name").GetComponent<Text>().color = unlocked ? UITheme.Cream : UITheme.Muted;
            pair.Value.transform.Find("Chapter").GetComponent<Text>().color = unlocked ? UITheme.Muted : new Color(0.45f, 0.48f, 0.44f, 1f);
            trackStatusLabels[pair.Key].text = unlocked ? (pair.Key == selectedTrackId ? "ВЫБРАН" : "ОТКРЫТ") : "ЗАКРЫТО";
            trackStatusLabels[pair.Key].color = unlocked ? UITheme.Brass : UITheme.Danger;
        }
        foreach (var pair in modeButtons)
        {
            bool active = pair.Key == selectedRaceType;
            pair.Value.image.color = active ? UITheme.Brass : UITheme.Panel;
            pair.Value.transform.Find("Label").GetComponent<Text>().color = active ? UITheme.Dark : UITheme.Cream;
        }
        Transform modeDescription = root.Find("ModeDescription");
        if (modeDescription != null) modeDescription.GetComponent<Text>().text = RaceTypeInfo.Description(selectedRaceType);
        if (inspected != null)
        {
            bool unlocked = GameManager.Instance.IsTrackUnlocked(inspected.id);
            detailTitle.text = inspected.displayName;
            detailChapter.text = $"{inspected.chapter}  ·  {inspected.Location}";
            detailDescription.text = inspected.description;
            detailSpecs.text = $"ДИСТАНЦИЯ  {inspected.ApproximateLengthMeters / 1000f:F1} км\nКРУГИ  {RaceTypeInfo.Laps(inspected, selectedRaceType)}   СОПЕРНИКИ  {RaceTypeInfo.AiCount(inspected, selectedRaceType)}\nЛИМИТ  {RaceHUD.FormatTime(inspected.timeLimit)}   НАГРАДА  {inspected.reward} ₽";
            detailRequirement.text = unlocked ? "Маршрут открыт" : GameManager.Instance.GetTrackUnlockRequirement(inspected.id);
            detailRequirement.color = unlocked ? UITheme.Success : UITheme.Danger;
        }
        if (startButton != null)
        {
            bool canStart = inspectedTrackId == selectedTrackId && GameManager.Instance.IsTrackUnlocked(selectedTrackId) && GameManager.Instance.CanStartRace();
            startButton.interactable = canStart;
            startButton.image.color = canStart ? UITheme.Brass : UITheme.Panel;
            startButton.transform.Find("Label").GetComponent<Text>().color = canStart ? UITheme.Dark : UITheme.Muted;
        }
    }

    private void StartRace()
    {
        if (inspectedTrackId != selectedTrackId)
        {
            statusText.color = UITheme.Danger;
            statusText.text = "Выберите открытый маршрут для старта";
            return;
        }
        if (!GameManager.Instance.IsTrackUnlocked(selectedTrackId))
        {
            statusText.color = UITheme.Danger;
            statusText.text = GameManager.Instance.GetTrackUnlockRequirement(selectedTrackId);
            return;
        }
        if (!GameManager.Instance.CanStartRace())
        {
            statusText.color = UITheme.Danger;
            statusText.text = "Сначала выберите исправный автомобиль в гараже";
            return;
        }
        if (!GameManager.Instance.SelectRace(selectedTrackId, selectedRaceType)) return;
        GameManager.Instance.ResetRaceTransientState();
        Close();
        SceneManager.LoadScene("PreRace");
    }

    private void Update()
    {
        if (UnityEngine.InputSystem.Keyboard.current == null) return;
        if (UnityEngine.InputSystem.Keyboard.current.escapeKey.wasPressedThisFrame) Close();
        else if (UnityEngine.InputSystem.Keyboard.current.enterKey.wasPressedThisFrame) StartRace();
    }

    public void Close()
    {
        if (Instance == this) Instance = null;
        if (root != null) Destroy(root.gameObject);
        Destroy(gameObject);
    }
}
