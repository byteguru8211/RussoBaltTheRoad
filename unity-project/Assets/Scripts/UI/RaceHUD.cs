using UnityEngine;
using UnityEngine.UI;

public class RaceHUD : MonoBehaviour
{
    public RaceManager race;
    private Text speedText;
    private Text gearText;
    private Text rpmText;
    private Text fuelText;
    private Text tempText;
    private Text damageText;
    private Text positionText;
    private Text timeText;
    private Text checkpointText;
    private Text roadbookText;
    private Text countdownText;
    private Text messageText;
    private Image fuelBar;
    private Image tempBar;
    private Image damageBar;

    public static RaceHUD Create(RaceManager manager)
    {
        GameObject go = new GameObject("RaceHUD", typeof(RaceHUD));
        go.GetComponent<RaceHUD>().race = manager;
        go.GetComponent<RaceHUD>().Build();
        return go.GetComponent<RaceHUD>();
    }

    private void Build()
    {
        Canvas canvas = UIUtils.CreateCanvas("RaceHUDCanvas", 20);
        UIUtils.Panel(canvas.transform, "TopBar", UITheme.Dark, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -86), Vector2.zero);
        UIUtils.Text(canvas.transform, "Track", race.TrackData.displayName, 26, UITheme.Cream, TextAnchor.MiddleLeft, new Vector2(0.02f, 1), new Vector2(0.45f, 1), new Vector2(0, -76), new Vector2(0, -18));
        positionText = UIUtils.Text(canvas.transform, "Position", "1/5", 34, UITheme.Brass, TextAnchor.MiddleRight, new Vector2(0.70f, 1), new Vector2(0.82f, 1), new Vector2(0, -74), new Vector2(0, -20));
        timeText = UIUtils.Text(canvas.transform, "Time", "00:00.0", 30, UITheme.Cream, TextAnchor.MiddleRight, new Vector2(0.82f, 1), new Vector2(0.98f, 1), new Vector2(0, -74), new Vector2(0, -20));
        UIUtils.Button(canvas.transform, "Pause", "II", new Vector2(0.94f, 0.88f), new Vector2(0.99f, 0.96f), Vector2.zero, Vector2.zero, () => race.Pause(), 24);

        UIUtils.Panel(canvas.transform, "SpeedPanel", UITheme.Dark, new Vector2(0.02f, 0.05f), new Vector2(0.27f, 0.44f), Vector2.zero, Vector2.zero);
        speedText = UIUtils.Text(canvas.transform, "Speed", "0", 78, UITheme.Cream, TextAnchor.MiddleCenter, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 50), new Vector2(0, -20));
        UIUtils.Text(canvas.transform, "SpeedUnit", "км/ч", 22, UITheme.Muted, TextAnchor.MiddleCenter, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, 10), new Vector2(0, 45));
        gearText = UIUtils.Text(canvas.transform, "Gear", "1", 44, UITheme.Brass, TextAnchor.MiddleCenter, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, -35), new Vector2(0, 5));
        rpmText = UIUtils.Text(canvas.transform, "RPM", "600 rpm", 20, UITheme.Muted, TextAnchor.MiddleCenter, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0, -75), new Vector2(0, -40));

        UIUtils.Panel(canvas.transform, "StatusPanel", UITheme.Dark, new Vector2(0.29f, 0.05f), new Vector2(0.52f, 0.27f), Vector2.zero, Vector2.zero);
        fuelText = UIUtils.Text(canvas.transform, "Fuel", "Топливо 100%", 21, UITheme.Cream, TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(1, 1), new Vector2(18, -54), new Vector2(-18, -28));
        tempText = UIUtils.Text(canvas.transform, "Temp", "Температура 20°", 21, UITheme.Cream, TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(1, 1), new Vector2(18, -88), new Vector2(-18, -62));
        damageText = UIUtils.Text(canvas.transform, "Damage", "Повреждения 0%", 21, UITheme.Cream, TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(1, 1), new Vector2(18, -122), new Vector2(-18, -96));
        fuelBar = Bar(canvas.transform, "FuelBar", new Vector2(0.30f, 0.14f), new Vector2(0.40f, 0.155f), UITheme.Brass);
        tempBar = Bar(canvas.transform, "TempBar", new Vector2(0.30f, 0.095f), new Vector2(0.40f, 0.11f), UITheme.Danger);
        damageBar = Bar(canvas.transform, "DamageBar", new Vector2(0.30f, 0.05f), new Vector2(0.40f, 0.065f), UITheme.Muted);
        UIUtils.Button(canvas.transform, "Repair", "Ремонт · 50", new Vector2(0.41f, 0.055f), new Vector2(0.51f, 0.12f), Vector2.zero, Vector2.zero, () => race.PlayerRepair(), 19);

        UIUtils.Panel(canvas.transform, "Roadbook", UITheme.Dark, new Vector2(0.73f, 0.05f), new Vector2(0.98f, 0.30f), Vector2.zero, Vector2.zero);
        checkpointText = UIUtils.Text(canvas.transform, "Checkpoint", "Контроль 1/12", 22, UITheme.Brass, TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(1, 1), new Vector2(18, -46), new Vector2(-18, -20));
        roadbookText = UIUtils.Text(canvas.transform, "Roadbook", "Прямо", 28, UITheme.Cream, TextAnchor.MiddleLeft, new Vector2(0, 0), new Vector2(1, 1), new Vector2(18, -94), new Vector2(-18, -54));
        UIUtils.Text(canvas.transform, "Controls", "W/S · газ/тормоз   A/D · руль   Shift · сцепление   1–3 · передачи   F · старт", 17, UITheme.Muted, TextAnchor.MiddleCenter, new Vector2(0.25f, 0.01f), new Vector2(0.75f, 0.045f), Vector2.zero, Vector2.zero);

        countdownText = UIUtils.Text(canvas.transform, "Countdown", "", 110, UITheme.Cream, TextAnchor.MiddleCenter, new Vector2(0.25f, 0.30f), new Vector2(0.75f, 0.70f), Vector2.zero, Vector2.zero);
        messageText = UIUtils.Text(canvas.transform, "Message", "", 32, UITheme.Brass, TextAnchor.MiddleCenter, new Vector2(0.25f, 0.72f), new Vector2(0.75f, 0.80f), Vector2.zero, Vector2.zero);
    }

    private Image Bar(Transform parent, string name, Vector2 min, Vector2 max, Color color)
    {
        UIUtils.Panel(parent, name + "Back", new Color(0.12f, 0.14f, 0.12f, 1f), min, max, Vector2.zero, Vector2.zero);
        return UIUtils.Panel(parent, name, color, min, max, Vector2.zero, Vector2.zero);
    }

    private void Update()
    {
        if (race == null || race.Player == null)
            return;
        VehicleController1912 vehicle = race.Player;
        speedText.text = Mathf.RoundToInt(vehicle.SpeedKmh).ToString();
        gearText.text = vehicle.Reverse ? "R" : vehicle.gear.ToString();
        rpmText.text = $"{Mathf.RoundToInt(vehicle.rpm)} rpm";
        fuelText.text = $"Топливо {Mathf.RoundToInt(vehicle.fuel)}%";
        tempText.text = $"Температура {Mathf.RoundToInt(vehicle.temperature)}°";
        damageText.text = $"Повреждения {Mathf.RoundToInt(vehicle.damage)}%";
        positionText.text = $"{race.CurrentPosition}/{race.RacersCount}";
        timeText.text = FormatTime(race.Elapsed);
        checkpointText.text = $"Контроль {Mathf.Min(race.CheckpointIndex + 1, race.TrackBuilder.checkpointCount)}/{race.TrackBuilder.checkpointCount}";
        fuelBar.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(vehicle.fuel / 100f), 1f);
        tempBar.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(vehicle.temperature / 120f), 1f);
        damageBar.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(vehicle.damage / 100f), 1f);
        roadbookText.text = race.NextInstruction;
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
        int minutes = Mathf.FloorToInt(seconds / 60f);
        int secs = Mathf.FloorToInt(seconds % 60f);
        int tenths = Mathf.FloorToInt((seconds % 1f) * 10f);
        return $"{minutes:00}:{secs:00}.{tenths}";
    }
}
