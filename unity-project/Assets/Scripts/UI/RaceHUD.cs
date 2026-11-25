using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class RaceHUD : MonoBehaviour
{
    public RaceManager race;
    private Canvas canvas;
    private CameraController1912 cameraController;
    private int lastCameraMode = -1;
    private float standingsTimer;
    private Text trackText;
    private Text lapText;
    private Text positionText;
    private Text timeText;
    private Text bestText;
    private Text deltaText;
    private Text speedText;
    private Text gearText;
    private Text rpmText;
    private Text fuelText;
    private Text tempText;
    private Text damageText;
    private Text checkpointText;
    private Text roadbookText;
    private Text cameraText;
    private Text countdownText;
    private Text messageText;
    private Image fuelBar;
    private Image tempBar;
    private Image damageBar;
    private HUDGaugeUI speedGauge;
    private HUDGaugeUI rpmGauge;
    private TrackMapUI minimap;
    private Image playerMarker;
    private Sprite markerSprite;
    private Texture2D markerTexture;
    private readonly Text[] standingNames = new Text[7];
    private readonly Text[] standingGaps = new Text[7];
    private readonly Text[] standingPlaces = new Text[7];
    private readonly Image[] standingRows = new Image[7];

    public static RaceHUD Create(RaceManager manager)
    {
        GameObject go = new GameObject("RaceHUD", typeof(RaceHUD));
        RaceHUD hud = go.GetComponent<RaceHUD>();
        hud.race = manager;
        hud.Build();
        return hud;
    }

    private void Build()
    {
        canvas = UIUtils.CreateCanvas("RaceHUDCanvas", 20);
        UIUtils.Panel(canvas.transform, "TopBar", UITheme.Dark, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -104f), Vector2.zero);
        UIUtils.Text(canvas.transform, "TrackLabel", $"RUSSO-BALT  ·  {RaceTypeInfo.DisplayName(GameManager.Instance.SelectedRaceType).ToUpperInvariant()}", 13, UITheme.Brass, TextAnchor.MiddleLeft, new Vector2(0.02f, 0.962f), new Vector2(0.34f, 0.993f), Vector2.zero, Vector2.zero);
        trackText = UIUtils.Text(canvas.transform, "Track", race.TrackData.displayName, 25, UITheme.Cream, TextAnchor.MiddleLeft, new Vector2(0.02f, 0.912f), new Vector2(0.34f, 0.963f), Vector2.zero, Vector2.zero, UITheme.TitleFont);
        lapText = UIUtils.Text(canvas.transform, "Lap", "КРУГ 1/1", 29, UITheme.Brass, TextAnchor.MiddleCenter, new Vector2(0.35f, 0.918f), new Vector2(0.49f, 0.982f), Vector2.zero, Vector2.zero);
        UIUtils.Text(canvas.transform, "PositionLabel", "ПОЗИЦИЯ", 12, UITheme.Muted, TextAnchor.MiddleCenter, new Vector2(0.50f, 0.962f), new Vector2(0.62f, 0.99f), Vector2.zero, Vector2.zero);
        positionText = UIUtils.Text(canvas.transform, "Position", "1/5", 34, UITheme.Cream, TextAnchor.MiddleCenter, new Vector2(0.50f, 0.912f), new Vector2(0.62f, 0.965f), Vector2.zero, Vector2.zero, UITheme.TitleFont);
        UIUtils.Text(canvas.transform, "TimeLabel", "ВРЕМЯ", 12, UITheme.Muted, TextAnchor.MiddleRight, new Vector2(0.64f, 0.962f), new Vector2(0.91f, 0.99f), Vector2.zero, Vector2.zero);
        timeText = UIUtils.Text(canvas.transform, "Time", "00:00.0", 30, UITheme.Cream, TextAnchor.MiddleRight, new Vector2(0.64f, 0.912f), new Vector2(0.91f, 0.965f), Vector2.zero, Vector2.zero);
        bestText = UIUtils.Text(canvas.transform, "Best", "ЛУЧШЕЕ  —:—.—", 14, UITheme.Muted, TextAnchor.MiddleRight, new Vector2(0.64f, 0.885f), new Vector2(0.79f, 0.915f), Vector2.zero, Vector2.zero);
        deltaText = UIUtils.Text(canvas.transform, "Delta", "", 14, UITheme.Brass, TextAnchor.MiddleRight, new Vector2(0.79f, 0.885f), new Vector2(0.91f, 0.915f), Vector2.zero, Vector2.zero);
        UIUtils.Button(canvas.transform, "Pause", "II", new Vector2(0.945f, 0.925f), new Vector2(0.99f, 0.985f), Vector2.zero, Vector2.zero, () => race.Pause(), 22);

        BuildStandings();
        BuildRoadbook();
        BuildMinimap();
        BuildVehicleStatus();
        BuildInstruments();

        UIUtils.Button(canvas.transform, "Repair", "Ремонт · 50", new Vector2(0.50f, 0.055f), new Vector2(0.65f, 0.12f), Vector2.zero, Vector2.zero, () => race.PlayerRepair(), 18);
        UIUtils.Text(canvas.transform, "Controls", "W/S · газ/тормоз   A/D · руль   SHIFT · сцепление   1–4 / R · передачи   F · старт\nSPACE · ручник   BACKSPACE · вернуть   C · камера   ESC · пауза   ·   ГЕЙМПАД: RT/LT, СТИКИ, Y/B/C", 13, UITheme.Muted, TextAnchor.MiddleCenter, new Vector2(0.25f, 0.006f), new Vector2(0.65f, 0.050f), Vector2.zero, Vector2.zero);
        countdownText = UIUtils.Text(canvas.transform, "Countdown", "", 120, UITheme.Cream, TextAnchor.MiddleCenter, new Vector2(0.25f, 0.36f), new Vector2(0.75f, 0.68f), Vector2.zero, Vector2.zero, UITheme.TitleFont);
        Outline countdownOutline = countdownText.gameObject.AddComponent<Outline>();
        countdownOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        countdownOutline.effectDistance = new Vector2(4f, 4f);
        messageText = UIUtils.Text(canvas.transform, "Message", "", 27, UITheme.Brass, TextAnchor.MiddleCenter, new Vector2(0.25f, 0.72f), new Vector2(0.75f, 0.79f), Vector2.zero, Vector2.zero);
        RefreshStandings();
    }

    private void BuildStandings()
    {
        UIUtils.Card(canvas.transform, "Standings", UITheme.Dark, new Vector2(0.018f, 0.32f), new Vector2(0.235f, 0.875f), Vector2.zero, Vector2.zero);
        UIUtils.Text(canvas.transform, "StandingsLabel", "ПОРЯДОК  /  ЗАЕЗД", 16, UITheme.Brass, TextAnchor.MiddleLeft, new Vector2(0.032f, 0.805f), new Vector2(0.22f, 0.855f), Vector2.zero, Vector2.zero);
        for (int i = 0; i < standingRows.Length; i++)
        {
            float top = 0.797f - i * 0.064f;
            standingRows[i] = UIUtils.Panel(canvas.transform, "StandingRow" + i, UITheme.Panel, new Vector2(0.027f, top - 0.058f), new Vector2(0.226f, top - 0.003f), Vector2.zero, Vector2.zero, false);
            standingPlaces[i] = UIUtils.Text(standingRows[i].transform, "Place", $"{i + 1}", 18, UITheme.Brass, TextAnchor.MiddleCenter, new Vector2(0.01f, 0f), new Vector2(0.13f, 1f), Vector2.zero, Vector2.zero);
            standingNames[i] = UIUtils.Text(standingRows[i].transform, "Name", "", 17, UITheme.Cream, TextAnchor.MiddleLeft, new Vector2(0.14f, 0f), new Vector2(0.76f, 1f), new Vector2(0f, 0f), new Vector2(-4f, 0f));
            standingGaps[i] = UIUtils.Text(standingRows[i].transform, "Gap", "", 13, UITheme.Muted, TextAnchor.MiddleRight, new Vector2(0.73f, 0f), new Vector2(0.98f, 1f), Vector2.zero, Vector2.zero);
        }
    }

    private void BuildRoadbook()
    {
        UIUtils.Card(canvas.transform, "Roadbook", UITheme.Dark, new Vector2(0.745f, 0.51f), new Vector2(0.982f, 0.64f), Vector2.zero, Vector2.zero);
        checkpointText = UIUtils.Text(canvas.transform, "Checkpoint", "КОНТРОЛЬ 1/12", 16, UITheme.Brass, TextAnchor.MiddleLeft, new Vector2(0.762f, 0.575f), new Vector2(0.966f, 0.62f), Vector2.zero, Vector2.zero);
        roadbookText = UIUtils.Text(canvas.transform, "Roadbook", "Прямо", 25, UITheme.Cream, TextAnchor.MiddleLeft, new Vector2(0.762f, 0.52f), new Vector2(0.966f, 0.575f), Vector2.zero, Vector2.zero, UITheme.TitleFont);
        UIUtils.Card(canvas.transform, "CameraCard", UITheme.Dark, new Vector2(0.54f, 0.525f), new Vector2(0.715f, 0.60f), Vector2.zero, Vector2.zero);
        cameraText = UIUtils.Text(canvas.transform, "Camera", "КАМЕРА · ПОГОНЯ", 16, UITheme.Brass, TextAnchor.MiddleCenter, new Vector2(0.545f, 0.53f), new Vector2(0.71f, 0.595f), Vector2.zero, Vector2.zero);
    }

    private void BuildMinimap()
    {
        UIUtils.Card(canvas.transform, "MinimapCard", UITheme.Dark, new Vector2(0.018f, 0.075f), new Vector2(0.235f, 0.30f), Vector2.zero, Vector2.zero);
        UIUtils.Text(canvas.transform, "MinimapLabel", "КАРТА МАРШРУТА", 14, UITheme.Brass, TextAnchor.MiddleLeft, new Vector2(0.032f, 0.258f), new Vector2(0.22f, 0.29f), Vector2.zero, Vector2.zero);
        RectTransform mapRect = UIUtils.Rect(canvas.transform, "Minimap", new Vector2(0.035f, 0.095f), new Vector2(0.218f, 0.252f), Vector2.zero, Vector2.zero);
        minimap = mapRect.gameObject.AddComponent<TrackMapUI>();
        minimap.SetWaypoints(race.TrackData.waypoints != null && race.TrackData.waypoints.Length > 1 ? race.TrackData.waypoints : race.TrackBuilder.Samples.ToArray());
        playerMarker = UIUtils.Panel(mapRect, "PlayerMarker", UITheme.Cream, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-8f, -8f), new Vector2(8f, 8f), false);
        CreateMarkerSprite();
        playerMarker.sprite = markerSprite;
    }

    private void BuildVehicleStatus()
    {
        UIUtils.Card(canvas.transform, "StatusPanel", UITheme.Dark, new Vector2(0.25f, 0.075f), new Vector2(0.47f, 0.27f), Vector2.zero, Vector2.zero);
        UIUtils.Text(canvas.transform, "StatusLabel", "СОСТОЯНИЕ АВТОМОБИЛЯ", 14, UITheme.Brass, TextAnchor.MiddleLeft, new Vector2(0.265f, 0.232f), new Vector2(0.455f, 0.26f), Vector2.zero, Vector2.zero);
        fuelText = UIUtils.Text(canvas.transform, "Fuel", "Топливо 100%", 16, UITheme.Cream, TextAnchor.MiddleLeft, new Vector2(0.265f, 0.192f), new Vector2(0.455f, 0.222f), Vector2.zero, Vector2.zero);
        fuelBar = Bar(canvas.transform, "FuelBar", new Vector2(0.265f, 0.183f), new Vector2(0.455f, 0.189f), UITheme.Brass);
        tempText = UIUtils.Text(canvas.transform, "Temp", "Температура 20°", 16, UITheme.Cream, TextAnchor.MiddleLeft, new Vector2(0.265f, 0.142f), new Vector2(0.455f, 0.172f), Vector2.zero, Vector2.zero);
        tempBar = Bar(canvas.transform, "TempBar", new Vector2(0.265f, 0.133f), new Vector2(0.455f, 0.139f), UITheme.Danger);
        damageText = UIUtils.Text(canvas.transform, "Damage", "Повреждения 0%", 16, UITheme.Cream, TextAnchor.MiddleLeft, new Vector2(0.265f, 0.092f), new Vector2(0.455f, 0.122f), Vector2.zero, Vector2.zero);
        damageBar = Bar(canvas.transform, "DamageBar", new Vector2(0.265f, 0.083f), new Vector2(0.455f, 0.089f), UITheme.Muted);
    }

    private void BuildInstruments()
    {
        UIUtils.Card(canvas.transform, "Instruments", UITheme.Dark, new Vector2(0.66f, 0.055f), new Vector2(0.985f, 0.49f), Vector2.zero, Vector2.zero);
        UIUtils.Text(canvas.transform, "SpeedLabel", "СКОРОСТЬ", 14, UITheme.Brass, TextAnchor.MiddleCenter, new Vector2(0.72f, 0.445f), new Vector2(0.94f, 0.475f), Vector2.zero, Vector2.zero);
        RectTransform speedRect = UIUtils.Rect(canvas.transform, "SpeedGauge", new Vector2(0.69f, 0.20f), new Vector2(0.98f, 0.47f), Vector2.zero, Vector2.zero);
        speedGauge = speedRect.gameObject.AddComponent<HUDGaugeUI>();
        speedGauge.SetRange(0f, 180f);
        speedText = UIUtils.Text(canvas.transform, "Speed", "0", 100, UITheme.Cream, TextAnchor.MiddleCenter, new Vector2(0.73f, 0.315f), new Vector2(0.94f, 0.415f), Vector2.zero, Vector2.zero, UITheme.TitleFont);
        UIUtils.Text(canvas.transform, "SpeedUnit", "КМ/Ч", 17, UITheme.Muted, TextAnchor.MiddleCenter, new Vector2(0.74f, 0.275f), new Vector2(0.93f, 0.315f), Vector2.zero, Vector2.zero);
        gearText = UIUtils.Text(canvas.transform, "Gear", "1", 58, UITheme.Brass, TextAnchor.MiddleCenter, new Vector2(0.675f, 0.09f), new Vector2(0.775f, 0.20f), Vector2.zero, Vector2.zero, UITheme.TitleFont);
        UIUtils.Text(canvas.transform, "GearLabel", "ПЕРЕДАЧА", 11, UITheme.Muted, TextAnchor.MiddleCenter, new Vector2(0.675f, 0.065f), new Vector2(0.775f, 0.095f), Vector2.zero, Vector2.zero);
        RectTransform rpmRect = UIUtils.Rect(canvas.transform, "RPMGauge", new Vector2(0.80f, 0.065f), new Vector2(0.97f, 0.235f), Vector2.zero, Vector2.zero);
        rpmGauge = rpmRect.gameObject.AddComponent<HUDGaugeUI>();
        rpmGauge.SetRange(0f, 2400f);
        rpmGauge.SetTickCount(7);
        rpmText = UIUtils.Text(canvas.transform, "RPM", "600 rpm", 22, UITheme.Cream, TextAnchor.MiddleCenter, new Vector2(0.825f, 0.09f), new Vector2(0.95f, 0.145f), Vector2.zero, Vector2.zero);
        UIUtils.Text(canvas.transform, "RPMLabel", "ОБОРОТЫ  /  2400", 10, UITheme.Muted, TextAnchor.MiddleCenter, new Vector2(0.82f, 0.145f), new Vector2(0.955f, 0.18f), Vector2.zero, Vector2.zero);
    }

    private Image Bar(Transform parent, string name, Vector2 min, Vector2 max, Color color)
    {
        Image background = UIUtils.Panel(parent, name + "Back", new Color(0.12f, 0.14f, 0.12f, 1f), min, max, Vector2.zero, Vector2.zero, false);
        return UIUtils.Panel(background.transform, name, color, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
    }

    private void CreateMarkerSprite()
    {
        markerTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        Color clear = new Color(1f, 1f, 1f, 0f);
        Color fill = Color.white;
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                bool inside = y >= 3 && y <= 30 && Mathf.Abs(x - 15.5f) <= (31 - y) * 0.43f;
                markerTexture.SetPixel(x, y, inside ? fill : clear);
            }
        }
        markerTexture.Apply();
        markerSprite = Sprite.Create(markerTexture, new Rect(0f, 0f, 32f, 32f), new Vector2(0.5f, 0.5f), 32f);
    }

    private void Update()
    {
        if (race == null) return;
        int laps = Mathf.Max(1, GameManager.Instance.GetRaceLaps());
        int lap = Mathf.Clamp(race.CompletedLaps + 1, 1, laps);
        lapText.text = GameManager.Instance.SelectedRaceType == RaceType.Checkpoint ? "МАРШРУТ" : $"КРУГ {lap}/{laps}";
        positionText.text = $"{race.CurrentPosition}/{Mathf.Max(1, race.RacersCount)}";
        timeText.text = FormatTime(race.Elapsed);
        float best = GameManager.Instance.GetBestTime(race.TrackData.id);
        bestText.text = best > 0f ? $"ЛУЧШЕЕ  {FormatTime(best)}" : "ЛУЧШЕЕ  —:—.—";
        deltaText.text = best > 0f && race.State == RaceManager.RaceState.Racing ? $"{(race.Elapsed - best >= 0f ? "+" : "−")}{FormatTime(Mathf.Abs(race.Elapsed - best))}" : "";
        deltaText.color = best > 0f && race.Elapsed <= best ? UITheme.Success : UITheme.Brass;
        int checkpointCount = Mathf.Max(1, race.TrackBuilder != null ? race.TrackBuilder.checkpointCount : 12);
        int checkpointNumber = race.CheckpointIndex >= checkpointCount ? checkpointCount : Mathf.Clamp(race.CheckpointIndex, 1, checkpointCount);
        checkpointText.text = $"КОНТРОЛЬ {checkpointNumber}/{checkpointCount}";
        roadbookText.text = race.NextInstruction;

        cameraController = Camera.main != null ? Camera.main.GetComponent<CameraController1912>() : null;
        if (cameraController != null)
        {
            if (cameraController.mode != lastCameraMode)
            {
                lastCameraMode = cameraController.mode;
                GameManager.Instance.SelectedCameraMode = lastCameraMode;
            }
            cameraText.text = cameraController.mode switch
            {
                0 => "КАМЕРА · ПОГОНЯ",
                1 => "КАМЕРА · КАПОТ",
                2 => "КАМЕРА · САЛОН",
                3 => "КАМЕРА · КИНО",
                _ => "КАМЕРА"
            };
        }

        VehicleController1912 vehicle = race.Player;
        if (vehicle != null)
        {
            speedText.text = Mathf.RoundToInt(vehicle.SpeedKmh).ToString();
            gearText.text = vehicle.Reverse ? "R" : vehicle.gear.ToString();
            rpmText.text = $"{Mathf.RoundToInt(vehicle.rpm)} rpm";
            fuelText.text = $"Топливо {Mathf.RoundToInt(vehicle.fuel)}%";
            tempText.text = $"Температура {Mathf.RoundToInt(vehicle.temperature)}°";
            damageText.text = $"Повреждения {Mathf.RoundToInt(vehicle.damage)}%";
            speedGauge.SetValue(vehicle.SpeedKmh);
            rpmGauge.SetValue(vehicle.rpm);
            fuelBar.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(vehicle.fuel / 100f), 1f);
            tempBar.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(vehicle.temperature / 120f), 1f);
            damageBar.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(vehicle.damage / 100f), 1f);
            if (minimap != null && playerMarker != null)
            {
                playerMarker.rectTransform.anchoredPosition = minimap.WorldToMapPoint(vehicle.transform.position);
                Vector3 forward = vehicle.transform.forward;
                playerMarker.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg);
            }
        }

        standingsTimer += Time.unscaledDeltaTime;
        if (standingsTimer >= 0.3f)
        {
            standingsTimer = 0f;
            RefreshStandings();
        }
    }

    private void RefreshStandings()
    {
        List<RaceStandings.Standing> rows = RaceStandings.Build(race);
        for (int i = 0; i < standingRows.Length; i++)
        {
            bool visible = i < rows.Count;
            standingRows[i].gameObject.SetActive(visible);
            if (!visible) continue;
            RaceStandings.Standing row = rows[i];
            standingPlaces[i].text = row.Place.ToString();
            standingNames[i].text = Truncate(row.Name, 16);
            standingGaps[i].text = row.Place == 1 ? "ЛИДЕР" : $"+{Mathf.RoundToInt(row.GapMeters)} м";
            standingRows[i].color = row.IsPlayer ? UITheme.Raised : UITheme.Panel;
            standingNames[i].color = row.IsPlayer ? UITheme.Brass : UITheme.Cream;
            standingGaps[i].color = row.IsPlayer ? UITheme.Brass : UITheme.Muted;
        }
    }

    private static string Truncate(string value, int length)
    {
        if (string.IsNullOrEmpty(value)) return "Гонщик";
        return value.Length <= length ? value : value.Substring(0, length - 1) + "…";
    }

    public void SetCountdown(string value) => countdownText.text = value;
    public void SetMessage(string value, float duration = 2.5f)
    {
        CancelInvoke(nameof(ClearMessage));
        messageText.text = value;
        Invoke(nameof(ClearMessage), duration);
    }
    private void ClearMessage() => messageText.text = "";
    public static string FormatTime(float seconds)
    {
        if (float.IsNaN(seconds) || float.IsInfinity(seconds)) seconds = 0f;
        seconds = Mathf.Max(0f, seconds);
        int minutes = Mathf.FloorToInt(seconds / 60f);
        int secs = Mathf.FloorToInt(seconds % 60f);
        int tenths = Mathf.FloorToInt((seconds % 1f) * 10f);
        return $"{minutes:00}:{secs:00}.{tenths}";
    }

    private void OnDestroy()
    {
        if (markerSprite != null) Destroy(markerSprite);
        if (markerTexture != null) Destroy(markerTexture);
    }
}
