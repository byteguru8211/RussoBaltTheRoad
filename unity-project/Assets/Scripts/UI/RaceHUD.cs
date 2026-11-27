using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class RaceHUD : MonoBehaviour
{
    public RaceManager race;
    private Canvas canvas;
    private float standingsTimer;
    private float controlsTimer;
    private RaceManager.RaceState lastPhase = (RaceManager.RaceState)(-1);
    private Text trackText;
    private Text lapText;
    private Text positionText;
    private Text timeText;
    private Text bestText;
    private Text deltaText;
    private Text speedText;
    private Text gearText;
    private Text boostText;
    private Text rpmText;
    private Text engineText;
    private Text tempText;
    private Text damageText;
    private Text checkpointText;
    private Text roadbookText;
    private Text countdownText;
    private Text messageText;
    private Text controlsText;
    private Image tempBar;
    private Image damageBar;
    private Image boostBar;
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
    private readonly string[] raceplayObjects = { "Standings", "MinimapCard", "StatusPanel", "Instruments", "Roadbook", "Repair", "Controls" };

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
        UIUtils.Panel(canvas.transform, "TopBar", UITheme.HudDark, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -84f), Vector2.zero);
        UIUtils.Panel(canvas.transform, "TopRule", new Color(0.77f, 0.63f, 0.36f, 0.55f), new Vector2(0f, 0.920f), new Vector2(1f, 0.923f), Vector2.zero, Vector2.zero, false);
        UIUtils.Text(canvas.transform, "TrackLabel", $"RUSSO-BALT  ·  {RaceTypeInfo.DisplayName(GameManager.Instance.SelectedRaceType).ToUpperInvariant()}", 12, UITheme.Brass, TextAnchor.MiddleLeft, new Vector2(0.02f, 0.957f), new Vector2(0.34f, 0.987f), Vector2.zero, Vector2.zero);
        trackText = UIUtils.Text(canvas.transform, "Track", race.TrackData.displayName, 22, UITheme.Cream, TextAnchor.MiddleLeft, new Vector2(0.02f, 0.925f), new Vector2(0.34f, 0.958f), Vector2.zero, Vector2.zero, UITheme.TitleFont);
        lapText = UIUtils.Text(canvas.transform, "Lap", "КРУГ 1/1", 26, UITheme.Brass, TextAnchor.MiddleCenter, new Vector2(0.35f, 0.932f), new Vector2(0.49f, 0.982f), Vector2.zero, Vector2.zero);
        UIUtils.Text(canvas.transform, "PositionLabel", "ПОЗИЦИЯ", 11, UITheme.Muted, TextAnchor.MiddleCenter, new Vector2(0.50f, 0.958f), new Vector2(0.62f, 0.987f), Vector2.zero, Vector2.zero);
        positionText = UIUtils.Text(canvas.transform, "Position", "1/5", 30, UITheme.Cream, TextAnchor.MiddleCenter, new Vector2(0.50f, 0.928f), new Vector2(0.62f, 0.962f), Vector2.zero, Vector2.zero, UITheme.TitleFont);
        UIUtils.Text(canvas.transform, "TimeLabel", "ВРЕМЯ", 11, UITheme.Muted, TextAnchor.MiddleRight, new Vector2(0.64f, 0.958f), new Vector2(0.91f, 0.987f), Vector2.zero, Vector2.zero);
        timeText = UIUtils.Text(canvas.transform, "Time", "00:00.0", 27, UITheme.Cream, TextAnchor.MiddleRight, new Vector2(0.64f, 0.928f), new Vector2(0.91f, 0.962f), Vector2.zero, Vector2.zero);
        bestText = UIUtils.Text(canvas.transform, "Best", "ЛУЧШЕЕ  —:—.—", 12, UITheme.Muted, TextAnchor.MiddleRight, new Vector2(0.64f, 0.894f), new Vector2(0.79f, 0.918f), Vector2.zero, Vector2.zero);
        deltaText = UIUtils.Text(canvas.transform, "Delta", "", 12, UITheme.Brass, TextAnchor.MiddleRight, new Vector2(0.79f, 0.894f), new Vector2(0.91f, 0.918f), Vector2.zero, Vector2.zero);
        UIUtils.Button(canvas.transform, "Pause", "II", new Vector2(0.945f, 0.935f), new Vector2(0.99f, 0.985f), Vector2.zero, Vector2.zero, () => race.Pause(), 20);

        BuildStandings();
        BuildRoadbook();
        BuildMinimap();
        BuildVehicleStatus();
        BuildInstruments();

        UIUtils.Button(canvas.transform, "Repair", "Ремонт · 50", new Vector2(0.45f, 0.055f), new Vector2(0.58f, 0.105f), Vector2.zero, Vector2.zero, () => race.PlayerRepair(), 16);
        controlsText = UIUtils.Text(canvas.transform, "Controls", "W/S · газ/тормоз   A/D · руль   SHIFT · сцепление   1–4 / R · передачи\nSPACE · ручник   N · усиление   BACKSPACE · вернуть   C · камера   ESC · пауза   ·   ГЕЙМПАД: RT/LT, СТИКИ, Y/B/C", 11, UITheme.Muted, TextAnchor.MiddleCenter, new Vector2(0.25f, 0.003f), new Vector2(0.65f, 0.035f), Vector2.zero, Vector2.zero);
        countdownText = UIUtils.Text(canvas.transform, "Countdown", "", 90, UITheme.Cream, TextAnchor.MiddleCenter, new Vector2(0.30f, 0.38f), new Vector2(0.70f, 0.62f), Vector2.zero, Vector2.zero, UITheme.TitleFont);
        Outline countdownOutline = countdownText.gameObject.AddComponent<Outline>();
        countdownOutline.effectColor = new Color(0f, 0f, 0f, 0.85f);
        countdownOutline.effectDistance = new Vector2(4f, 4f);
        messageText = UIUtils.Text(canvas.transform, "Message", "", 24, UITheme.Brass, TextAnchor.MiddleCenter, new Vector2(0.25f, 0.72f), new Vector2(0.75f, 0.78f), Vector2.zero, Vector2.zero);
        RefreshStandings();
        UpdatePhaseVisibility();
    }

    private void BuildStandings()
    {
        UIUtils.Card(canvas.transform, "Standings", UITheme.HudDark, new Vector2(0.015f, 0.43f), new Vector2(0.19f, 0.86f), Vector2.zero, Vector2.zero);
        UIUtils.Text(canvas.transform, "StandingsLabel", "ПОРЯДОК / ЗАЕЗД", 14, UITheme.Brass, TextAnchor.MiddleLeft, new Vector2(0.027f, 0.815f), new Vector2(0.18f, 0.85f), Vector2.zero, Vector2.zero);
        for (int i = 0; i < standingRows.Length; i++)
        {
            float top = 0.807f - i * 0.052f;
            standingRows[i] = UIUtils.Panel(canvas.transform, "StandingRow" + i, UITheme.HudPanel, new Vector2(0.023f, top - 0.047f), new Vector2(0.182f, top - 0.003f), Vector2.zero, Vector2.zero, false);
            standingPlaces[i] = UIUtils.Text(standingRows[i].transform, "Place", $"{i + 1}", 16, UITheme.Brass, TextAnchor.MiddleCenter, new Vector2(0.01f, 0f), new Vector2(0.13f, 1f), Vector2.zero, Vector2.zero);
            standingNames[i] = UIUtils.Text(standingRows[i].transform, "Name", "", 15, UITheme.Cream, TextAnchor.MiddleLeft, new Vector2(0.14f, 0f), new Vector2(0.76f, 1f), Vector2.zero, new Vector2(-4f, 0f));
            standingGaps[i] = UIUtils.Text(standingRows[i].transform, "Gap", "", 11, UITheme.Muted, TextAnchor.MiddleRight, new Vector2(0.73f, 0f), new Vector2(0.98f, 1f), Vector2.zero, Vector2.zero);
        }
    }

    private void BuildRoadbook()
    {
        UIUtils.Card(canvas.transform, "Roadbook", UITheme.HudDark, new Vector2(0.755f, 0.47f), new Vector2(0.985f, 0.575f), Vector2.zero, Vector2.zero);
        checkpointText = UIUtils.Text(canvas.transform, "Checkpoint", "КОНТРОЛЬ 1/12", 13, UITheme.Brass, TextAnchor.MiddleLeft, new Vector2(0.77f, 0.525f), new Vector2(0.97f, 0.56f), Vector2.zero, Vector2.zero);
        roadbookText = UIUtils.Text(canvas.transform, "Roadbook", "Прямо", 22, UITheme.Cream, TextAnchor.MiddleLeft, new Vector2(0.77f, 0.478f), new Vector2(0.97f, 0.525f), Vector2.zero, Vector2.zero, UITheme.TitleFont);
    }

    private void BuildMinimap()
    {
        UIUtils.Card(canvas.transform, "MinimapCard", UITheme.HudDark, new Vector2(0.015f, 0.155f), new Vector2(0.19f, 0.405f), Vector2.zero, Vector2.zero);
        UIUtils.Text(canvas.transform, "MinimapLabel", "КАРТА МАРШРУТА", 12, UITheme.Brass, TextAnchor.MiddleLeft, new Vector2(0.027f, 0.365f), new Vector2(0.18f, 0.395f), Vector2.zero, Vector2.zero);
        RectTransform mapRect = UIUtils.Rect(canvas.transform, "Minimap", new Vector2(0.032f, 0.175f), new Vector2(0.175f, 0.355f), Vector2.zero, Vector2.zero);
        minimap = mapRect.gameObject.AddComponent<TrackMapUI>();
        minimap.SetWaypoints(race.TrackData.waypoints != null && race.TrackData.waypoints.Length > 1 ? race.TrackData.waypoints : race.TrackBuilder.Samples.ToArray());
        playerMarker = UIUtils.Panel(mapRect, "PlayerMarker", UITheme.Cream, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-7f, -7f), new Vector2(7f, 7f), false);
        CreateMarkerSprite();
        playerMarker.sprite = markerSprite;
    }

    private void BuildVehicleStatus()
    {
        UIUtils.Card(canvas.transform, "StatusPanel", UITheme.HudDark, new Vector2(0.215f, 0.06f), new Vector2(0.42f, 0.25f), Vector2.zero, Vector2.zero);
        UIUtils.Text(canvas.transform, "StatusLabel", "СОСТОЯНИЕ АВТОМОБИЛЯ", 12, UITheme.Brass, TextAnchor.MiddleLeft, new Vector2(0.23f, 0.207f), new Vector2(0.405f, 0.238f), Vector2.zero, Vector2.zero);
        engineText = UIUtils.Text(canvas.transform, "Engine", "Двигатель · РАБОТАЕТ", 16, UITheme.Success, TextAnchor.MiddleLeft, new Vector2(0.23f, 0.163f), new Vector2(0.405f, 0.197f), Vector2.zero, Vector2.zero);
        tempText = UIUtils.Text(canvas.transform, "Temp", "Температура 72°", 14, UITheme.Cream, TextAnchor.MiddleLeft, new Vector2(0.23f, 0.125f), new Vector2(0.405f, 0.153f), Vector2.zero, Vector2.zero);
        tempBar = Bar(canvas.transform, "TempBar", new Vector2(0.23f, 0.116f), new Vector2(0.405f, 0.122f), UITheme.Danger);
        damageText = UIUtils.Text(canvas.transform, "Damage", "Повреждения 0%", 14, UITheme.Cream, TextAnchor.MiddleLeft, new Vector2(0.23f, 0.081f), new Vector2(0.405f, 0.109f), Vector2.zero, Vector2.zero);
        damageBar = Bar(canvas.transform, "DamageBar", new Vector2(0.23f, 0.072f), new Vector2(0.405f, 0.078f), UITheme.Muted);
    }

    private void BuildInstruments()
    {
        UIUtils.Card(canvas.transform, "Instruments", UITheme.HudDark, new Vector2(0.67f, 0.055f), new Vector2(0.985f, 0.42f), Vector2.zero, Vector2.zero);
        UIUtils.Text(canvas.transform, "SpeedLabel", "СКОРОСТЬ", 12, UITheme.Brass, TextAnchor.MiddleCenter, new Vector2(0.72f, 0.375f), new Vector2(0.94f, 0.405f), Vector2.zero, Vector2.zero);
        RectTransform speedRect = UIUtils.Rect(canvas.transform, "SpeedGauge", new Vector2(0.70f, 0.17f), new Vector2(0.97f, 0.39f), Vector2.zero, Vector2.zero);
        speedGauge = speedRect.gameObject.AddComponent<HUDGaugeUI>();
        speedGauge.SetRange(0f, 180f);
        speedText = UIUtils.Text(canvas.transform, "Speed", "0", 64, UITheme.Cream, TextAnchor.MiddleCenter, new Vector2(0.74f, 0.265f), new Vector2(0.93f, 0.35f), Vector2.zero, Vector2.zero, UITheme.TitleFont);
        UIUtils.Text(canvas.transform, "SpeedUnit", "КМ/Ч", 14, UITheme.Muted, TextAnchor.MiddleCenter, new Vector2(0.74f, 0.225f), new Vector2(0.93f, 0.265f), Vector2.zero, Vector2.zero);
        boostText = UIUtils.Text(canvas.transform, "Boost", "УСИЛЕНИЕ · N 100%", 10, UITheme.Brass, TextAnchor.MiddleCenter, new Vector2(0.74f, 0.202f), new Vector2(0.94f, 0.224f), Vector2.zero, Vector2.zero);
        boostBar = Bar(canvas.transform, "BoostBar", new Vector2(0.74f, 0.195f), new Vector2(0.94f, 0.199f), UITheme.Success);
        gearText = UIUtils.Text(canvas.transform, "Gear", "1", 42, UITheme.Brass, TextAnchor.MiddleCenter, new Vector2(0.675f, 0.075f), new Vector2(0.775f, 0.15f), Vector2.zero, Vector2.zero, UITheme.TitleFont);
        UIUtils.Text(canvas.transform, "GearLabel", "ПЕРЕДАЧА", 10, UITheme.Muted, TextAnchor.MiddleCenter, new Vector2(0.675f, 0.062f), new Vector2(0.775f, 0.088f), Vector2.zero, Vector2.zero);
        RectTransform rpmRect = UIUtils.Rect(canvas.transform, "RPMGauge", new Vector2(0.79f, 0.065f), new Vector2(0.965f, 0.19f), Vector2.zero, Vector2.zero);
        rpmGauge = rpmRect.gameObject.AddComponent<HUDGaugeUI>();
        rpmGauge.SetRange(0f, 2400f);
        rpmGauge.SetTickCount(7);
        rpmText = UIUtils.Text(canvas.transform, "RPM", "650 rpm", 18, UITheme.Cream, TextAnchor.MiddleCenter, new Vector2(0.81f, 0.09f), new Vector2(0.945f, 0.13f), Vector2.zero, Vector2.zero);
        UIUtils.Text(canvas.transform, "RPMLabel", "ОБОРОТЫ / 2400", 9, UITheme.Muted, TextAnchor.MiddleCenter, new Vector2(0.81f, 0.132f), new Vector2(0.945f, 0.158f), Vector2.zero, Vector2.zero);
    }

    private Image Bar(Transform parent, string name, Vector2 min, Vector2 max, Color color)
    {
        Image background = UIUtils.Panel(parent, name + "Back", UITheme.HudPanel, min, max, Vector2.zero, Vector2.zero, false);
        return UIUtils.Panel(background.transform, name, color, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
    }

    private void CreateMarkerSprite()
    {
        markerTexture = new Texture2D(32, 32, TextureFormat.RGBA32, false);
        Color clear = new Color(1f, 1f, 1f, 0f);
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                bool inside = y >= 3 && y <= 30 && Mathf.Abs(x - 15.5f) <= (31 - y) * 0.43f;
                markerTexture.SetPixel(x, y, inside ? Color.white : clear);
            }
        }
        markerTexture.Apply();
        markerSprite = Sprite.Create(markerTexture, new Rect(0f, 0f, 32f, 32f), new Vector2(0.5f, 0.5f), 32f);
    }

    private void Update()
    {
        if (race == null) return;
        UpdatePhaseVisibility();
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

        VehicleController1912 vehicle = race.Player;
        if (vehicle != null)
        {
            float shownSpeed = Mathf.Abs(vehicle.SpeedKmh);
            speedText.text = Mathf.RoundToInt(shownSpeed).ToString();
            gearText.text = vehicle.Reverse ? "R" : vehicle.gear.ToString();
            rpmText.text = $"{Mathf.RoundToInt(vehicle.rpm)} rpm";
            engineText.text = vehicle.handCrankTimer > 0f ? "Двигатель · ПРОКРУТКА" : vehicle.engineRunning ? "Двигатель · РАБОТАЕТ" : "Двигатель · ЗАГЛУШЕН";
            engineText.color = vehicle.handCrankTimer > 0f ? UITheme.Brass : vehicle.engineRunning ? UITheme.Success : UITheme.Danger;
            tempText.text = $"Температура {Mathf.RoundToInt(vehicle.temperature)}°";
            damageText.text = $"Повреждения {Mathf.RoundToInt(vehicle.damage)}%";
            speedGauge.SetValue(shownSpeed);
            rpmGauge.SetValue(vehicle.rpm);
            boostText.text = $"УСИЛЕНИЕ · N {Mathf.RoundToInt(vehicle.BoostCharge * 100f)}%";
            boostText.color = vehicle.BoostActive ? UITheme.Success : UITheme.Brass;
            boostBar.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(vehicle.BoostCharge), 1f);
            boostBar.color = vehicle.BoostActive ? UITheme.Success : UITheme.Brass;
            tempBar.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(vehicle.temperature / 120f), 1f);
            damageBar.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(vehicle.damage / 100f), 1f);
            if (minimap != null && playerMarker != null)
            {
                playerMarker.rectTransform.anchoredPosition = minimap.WorldToMapPoint(vehicle.transform.position);
                Vector3 forward = vehicle.transform.forward;
                playerMarker.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg);
            }
        }

        if (race.State == RaceManager.RaceState.Racing)
        {
            controlsTimer += Time.unscaledDeltaTime;
            float alpha = Mathf.Clamp01(1f - (controlsTimer - 6f) / 2f);
            controlsText.color = new Color(UITheme.Muted.r, UITheme.Muted.g, UITheme.Muted.b, alpha);
        }
        standingsTimer += Time.unscaledDeltaTime;
        if (standingsTimer >= 0.3f)
        {
            standingsTimer = 0f;
            RefreshStandings();
        }
    }

    private void UpdatePhaseVisibility()
    {
        if (race == null || lastPhase == race.State) return;
        lastPhase = race.State;
        bool visible = race.State == RaceManager.RaceState.Racing || race.State == RaceManager.RaceState.Finished;
        foreach (string name in raceplayObjects)
        {
            Transform element = canvas != null ? canvas.transform.Find(name) : null;
            if (element != null) element.gameObject.SetActive(visible);
        }
        if (race.State == RaceManager.RaceState.Racing) controlsTimer = 0f;
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
            standingRows[i].color = row.IsPlayer ? UITheme.HudRaised : UITheme.HudPanel;
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
