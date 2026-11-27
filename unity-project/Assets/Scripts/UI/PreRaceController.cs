using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class PreRaceController : MonoBehaviour
{
    private enum Phase { Selecting, Loading }

    private Phase phase;
    private TrackData track;
    private VehicleController1912 showcaseVehicle;
    private Camera stageCamera;
    private Canvas canvas;
    private Image orbitSurface;
    private GameObject selectionPanel;
    private GameObject loadingPanel;
    private Text loadingText;
    private Image loadingFill;
    private Text weatherText;
    private Text timeText;
    private AsyncOperation loadOperation;
    private float yaw = 0.58f;
    private float orbitDirection = 1f;
    private float cameraHeight = 2.55f;
    private float cameraDistance = 7.8f;
    private Vector3 cameraVelocity;
    private Vector2 pointerPosition;
    private bool dragging;

    private void Start()
    {
        Time.timeScale = 1f;
        GameManager.Instance.ResetRaceTransientState();
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();
        GameManager.Instance.IsRaceActive = false;
        track = GameManager.LoadTrack(GameManager.Instance.SelectedTrackId);
        if (track == null || !GameManager.Instance.IsTrackUnlocked(track.id))
        {
            track = GameManager.LoadTrack("winter_departure");
            GameManager.Instance.SelectedTrackId = track.id;
        }
        SetupAtmosphere();
        SetupStage();
        SetupVehicle();
        SetupUI();
        phase = Phase.Selecting;
    }

    private void SetupAtmosphere()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.018f;
        RenderSettings.fogColor = new Color(0.025f, 0.045f, 0.052f);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientSkyColor = new Color(0.16f, 0.22f, 0.24f);
        RenderSettings.ambientEquatorColor = new Color(0.08f, 0.11f, 0.12f);
        RenderSettings.ambientGroundColor = new Color(0.025f, 0.03f, 0.03f);

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        Bloom bloom = ScriptableObject.CreateInstance<Bloom>();
        bloom.intensity.value = 0.65f;
        bloom.threshold.value = 0.85f;
        bloom.scatter.value = 0.62f;
        profile.components.Add(bloom);
        ColorAdjustments color = ScriptableObject.CreateInstance<ColorAdjustments>();
        color.postExposure.value = 0.35f;
        color.contrast.value = 18f;
        color.saturation.value = -4f;
        profile.components.Add(color);
        Vignette vignette = ScriptableObject.CreateInstance<Vignette>();
        vignette.intensity.value = 0.3f;
        vignette.smoothness.value = 0.45f;
        profile.components.Add(vignette);
        GameObject volumeObject = new GameObject("ShowroomVolume");
        Volume volume = volumeObject.AddComponent<Volume>();
        volume.profile = profile;
        volume.isGlobal = true;

        Light sun = FindAnyObjectByType<Light>();
        if (sun != null)
        {
            sun.type = LightType.Directional;
            sun.color = new Color(0.52f, 0.68f, 0.78f);
            sun.intensity = 0.45f;
            sun.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
        }
    }

    private void SetupStage()
    {
        stageCamera = Camera.main;
        if (stageCamera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            stageCamera = cameraObject.GetComponent<Camera>();
        }
        stageCamera.clearFlags = CameraClearFlags.SolidColor;
        stageCamera.backgroundColor = new Color(0.018f, 0.032f, 0.04f);
        stageCamera.fieldOfView = 47f;
        stageCamera.nearClipPlane = 0.1f;
        stageCamera.farClipPlane = 120f;
        stageCamera.allowHDR = true;
        UpdateShowroomCamera(1f, true);

        GameObject stage = Resources.Load<GameObject>("PreRace/PreRace_Paddock");
        if (stage == null)
        {
            Debug.LogError("PreRace paddock asset is missing.");
            return;
        }
        Instantiate(stage, Vector3.zero, Quaternion.identity);
        CreateSpot("ShowroomKey", new Vector3(4.5f, 7f, 3.5f), new Color(1f, 0.79f, 0.52f), 42f, true);
        CreateSpot("ShowroomRim", new Vector3(-5f, 5f, -2f), new Color(0.36f, 0.72f, 1f), 32f, false);
        CreateSpot("StartGateLight", new Vector3(0f, 6f, 8f), new Color(1f, 0.9f, 0.68f), 26f, false);
    }

    private void CreateSpot(string name, Vector3 position, Color color, float intensity, bool shadows)
    {
        GameObject lightObject = new GameObject(name, typeof(Light));
        lightObject.transform.position = position;
        Light light = lightObject.GetComponent<Light>();
        light.type = LightType.Spot;
        light.color = color;
        light.intensity = intensity;
        light.range = 24f;
        light.spotAngle = 72f;
        light.innerSpotAngle = 48f;
        light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
        lightObject.transform.LookAt(new Vector3(0f, 0.7f, 0f));
    }

    private void SetupVehicle()
    {
        string vehicleId = GameManager.Instance.SelectedVehicleId;
        if (!GameManager.Instance.Profile.unlockedVehicles.Contains(vehicleId))
            vehicleId = "russo_balt_c24_55";
        GameObject prefab = Resources.Load<GameObject>($"Vehicles/{vehicleId}");
        if (prefab == null)
            prefab = Resources.Load<GameObject>("Vehicles/russo_balt_c24_55");
        GameObject vehicleObject = Instantiate(prefab, new Vector3(0f, 0.27f, 0f), Quaternion.identity);
        vehicleObject.name = "ShowcaseCar";
        showcaseVehicle = vehicleObject.GetComponent<VehicleController1912>();
        showcaseVehicle.enabled = false;
        showcaseVehicle.engineRunning = true;
        showcaseVehicle.rpm = 650f;
        showcaseVehicle.fuel = 100f;
        showcaseVehicle.temperature = 24f;
        showcaseVehicle.damage = 0f;
        showcaseVehicle.gear = 1;
        vehicleObject.GetComponent<VehicleInput1912>().enabled = false;
        Rigidbody body = vehicleObject.GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        foreach (Collider collider in vehicleObject.GetComponentsInChildren<Collider>())
            collider.enabled = false;
    }

    private void SetupUI()
    {
        canvas = UIUtils.CreateCanvas("PreRaceCanvas", 20);
        UIUtils.Scrim(canvas.transform, "Atmosphere", new Color(0.008f, 0.018f, 0.024f, 0.3f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        orbitSurface = UIUtils.Panel(canvas.transform, "OrbitSurface", Color.clear, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        SetupOrbitInput(orbitSurface);

        selectionPanel = UIUtils.Rect(canvas.transform, "Selection", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
        Transform selection = selectionPanel.transform;
        UIUtils.Scrim(selection, "LeftShade", new Color(0.015f, 0.028f, 0.032f, 0.93f), new Vector2(0f, 0f), new Vector2(0.38f, 1f), Vector2.zero, Vector2.zero);
        UIUtils.Scrim(selection, "BottomShade", new Color(0.01f, 0.02f, 0.024f, 0.72f), new Vector2(0.38f, 0f), new Vector2(1f, 0.18f), Vector2.zero, Vector2.zero);
        UIUtils.Panel(selection, "TopRule", UITheme.Brass, new Vector2(0.04f, 0.935f), new Vector2(0.96f, 0.938f), Vector2.zero, Vector2.zero, false);
        UIUtils.Text(selection, "Kicker", "RUSSO-BALT  /  RACE CONTROL", 19, UITheme.Brass, TextAnchor.MiddleLeft, new Vector2(0.04f, 0.885f), new Vector2(0.52f, 0.925f), Vector2.zero, Vector2.zero);
        UIUtils.Text(selection, "TrackTitle", track.displayName.ToUpperInvariant(), 53, UITheme.Cream, TextAnchor.MiddleLeft, new Vector2(0.04f, 0.81f), new Vector2(0.68f, 0.89f), Vector2.zero, Vector2.zero, UITheme.TitleFont);
        UIUtils.Text(selection, "Chapter", track.chapter, 21, UITheme.Muted, TextAnchor.MiddleLeft, new Vector2(0.04f, 0.775f), new Vector2(0.45f, 0.815f), Vector2.zero, Vector2.zero);
        UIUtils.Text(selection, "Credits", $"РУБЛИ  {GameManager.Instance.Profile.credits:F0}", 20, UITheme.Brass, TextAnchor.MiddleRight, new Vector2(0.76f, 0.885f), new Vector2(0.96f, 0.925f), Vector2.zero, Vector2.zero);

        UIUtils.Card(selection, "VehicleCard", UITheme.Dark, new Vector2(0.04f, 0.49f), new Vector2(0.34f, 0.735f), Vector2.zero, Vector2.zero);
        UIUtils.Text(selection, "VehicleLabel", "ВАШ АВТОМОБИЛЬ", 18, UITheme.Brass, TextAnchor.MiddleLeft, new Vector2(0.06f, 0.675f), new Vector2(0.32f, 0.715f), Vector2.zero, Vector2.zero);
        UIUtils.Text(selection, "VehicleName", VehicleCatalog.DisplayName(GameManager.Instance.SelectedVehicleId), 30, UITheme.Cream, TextAnchor.MiddleLeft, new Vector2(0.06f, 0.59f), new Vector2(0.32f, 0.675f), Vector2.zero, Vector2.zero, UITheme.TitleFont);
        UIUtils.Text(selection, "VehicleSpecs", VehicleCatalog.Specifications(GameManager.Instance.SelectedVehicleId), 19, UITheme.Muted, TextAnchor.UpperLeft, new Vector2(0.06f, 0.505f), new Vector2(0.32f, 0.585f), Vector2.zero, Vector2.zero);
        UIUtils.Panel(selection, "CardRule", new Color(0.77f, 0.63f, 0.36f, 0.4f), new Vector2(0.06f, 0.575f), new Vector2(0.32f, 0.578f), Vector2.zero, Vector2.zero, false);

        UIUtils.Card(selection, "RouteCard", UITheme.Dark, new Vector2(0.04f, 0.19f), new Vector2(0.34f, 0.455f), Vector2.zero, Vector2.zero);
        UIUtils.Text(selection, "RouteLabel", RaceTypeInfo.DisplayName(GameManager.Instance.SelectedRaceType).ToUpperInvariant(), 18, UITheme.Brass, TextAnchor.MiddleLeft, new Vector2(0.06f, 0.395f), new Vector2(0.32f, 0.435f), Vector2.zero, Vector2.zero);
        UIUtils.Text(selection, "Description", track.description, 20, UITheme.Cream, TextAnchor.UpperLeft, new Vector2(0.06f, 0.285f), new Vector2(0.32f, 0.39f), Vector2.zero, Vector2.zero);
        UIUtils.Text(selection, "RaceSpecs", $"КРУГИ  {GameManager.Instance.GetRaceLaps()}     СОПЕРНИКИ  {GameManager.Instance.GetRaceAiCount()}\nЛИМИТ  {FormatTime(track.timeLimit)}     НАГРАДА  {track.reward} ₽", 18, UITheme.Brass, TextAnchor.MiddleLeft, new Vector2(0.06f, 0.205f), new Vector2(0.32f, 0.275f), Vector2.zero, Vector2.zero);

        UIUtils.Button(selection, "Back", "←  ГАРАЖ", new Vector2(0.04f, 0.065f), new Vector2(0.17f, 0.135f), Vector2.zero, Vector2.zero, BackToGarage, 22);
        UIUtils.Button(selection, "Start", "НАЧАТЬ ЗАЕЗД  →", new Vector2(0.185f, 0.055f), new Vector2(0.43f, 0.145f), Vector2.zero, Vector2.zero, BeginStart, 29);
        UIUtils.Text(selection, "OrbitHint", "ЛКМ — ОСМОТР   ·   КОЛЕСО — ПРИБЛИЖЕНИЕ   ·   ENTER — СТАРТ", 17, UITheme.Muted, TextAnchor.MiddleRight, new Vector2(0.49f, 0.065f), new Vector2(0.96f, 0.115f), Vector2.zero, Vector2.zero);
        UIUtils.Text(selection, "StageLabel", "РИГСКАЯ МАСТЕРСКАЯ  ·  1912", 17, UITheme.Brass, TextAnchor.MiddleRight, new Vector2(0.58f, 0.825f), new Vector2(0.96f, 0.86f), Vector2.zero, Vector2.zero);

        loadingPanel = UIUtils.Rect(canvas.transform, "LoadingPanel", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject;
        loadingPanel.SetActive(false);
        UIUtils.Scrim(loadingPanel.transform, "Shade", new Color(0.008f, 0.018f, 0.022f, 0.97f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        UIUtils.Text(loadingPanel.transform, "Title", "НА ТРАССУ", 58, UITheme.Cream, TextAnchor.MiddleCenter, new Vector2(0.25f, 0.58f), new Vector2(0.75f, 0.7f), Vector2.zero, Vector2.zero, UITheme.TitleFont);
        loadingText = UIUtils.Text(loadingPanel.transform, "Status", "Подготовка трассы…", 22, UITheme.Muted, TextAnchor.MiddleCenter, new Vector2(0.25f, 0.48f), new Vector2(0.75f, 0.53f), Vector2.zero, Vector2.zero);
        UIUtils.Panel(loadingPanel.transform, "ProgressBack", UITheme.Panel, new Vector2(0.25f, 0.405f), new Vector2(0.75f, 0.425f), Vector2.zero, Vector2.zero, false);
        loadingFill = UIUtils.Panel(loadingPanel.transform, "ProgressFill", UITheme.Brass, new Vector2(0.25f, 0.405f), new Vector2(0.25f, 0.425f), Vector2.zero, Vector2.zero, false);
        UIUtils.Text(loadingPanel.transform, "Tip", "Двигатель уже заведён · C — камера · R — задний ход.", 18, UITheme.Muted, TextAnchor.MiddleCenter, new Vector2(0.2f, 0.31f), new Vector2(0.8f, 0.36f), Vector2.zero, Vector2.zero);
        BuildConditionPanel();
    }

    private void BuildConditionPanel()
    {
        UIUtils.Card(selectionPanel.transform, "Conditions", UITheme.HudDark, new Vector2(0.48f, 0.19f), new Vector2(0.96f, 0.285f), Vector2.zero, Vector2.zero);
        UIUtils.Text(selectionPanel.transform, "ConditionsLabel", "УСЛОВИЯ ЗАЕЗДА", 13, UITheme.Brass, TextAnchor.MiddleLeft, new Vector2(0.50f, 0.247f), new Vector2(0.94f, 0.275f), Vector2.zero, Vector2.zero);
        UIUtils.Button(selectionPanel.transform, "Weather", "", new Vector2(0.50f, 0.198f), new Vector2(0.72f, 0.24f), Vector2.zero, Vector2.zero, CycleWeather, 14);
        UIUtils.Button(selectionPanel.transform, "TimeOfDay", "", new Vector2(0.74f, 0.198f), new Vector2(0.94f, 0.24f), Vector2.zero, Vector2.zero, CycleTime, 14);
        weatherText = UIUtils.Text(selectionPanel.transform, "WeatherText", "", 14, UITheme.Cream, TextAnchor.MiddleCenter, new Vector2(0.50f, 0.198f), new Vector2(0.72f, 0.24f), Vector2.zero, Vector2.zero);
        timeText = UIUtils.Text(selectionPanel.transform, "TimeText", "", 14, UITheme.Cream, TextAnchor.MiddleCenter, new Vector2(0.74f, 0.198f), new Vector2(0.94f, 0.24f), Vector2.zero, Vector2.zero);
        RefreshConditionLabels();
    }

    private void CycleWeather()
    {
        SettingsData settings = GameManager.Instance.Settings;
        settings.weather = (WeatherPreset)(((int)settings.weather + 1) % Enum.GetValues(typeof(WeatherPreset)).Length);
        RefreshConditionLabels();
        GameManager.Instance.Save();
    }

    private void CycleTime()
    {
        SettingsData settings = GameManager.Instance.Settings;
        settings.timeOfDay = (TimeOfDayPreset)(((int)settings.timeOfDay + 1) % Enum.GetValues(typeof(TimeOfDayPreset)).Length);
        RefreshConditionLabels();
        GameManager.Instance.Save();
    }

    private void RefreshConditionLabels()
    {
        SettingsData settings = GameManager.Instance.Settings;
        if (weatherText != null) weatherText.text = "Погода · " + WeatherName(settings.weather);
        if (timeText != null) timeText.text = "Время · " + TimeName(settings.timeOfDay);
    }

    private static string WeatherName(WeatherPreset weather) => weather switch
    {
        WeatherPreset.Clear => "Ясно",
        WeatherPreset.Cloudy => "Облачно",
        WeatherPreset.Rain => "Дождь",
        WeatherPreset.Snow => "Снег",
        WeatherPreset.Fog => "Туман",
        _ => "Авто"
    };

    private static string TimeName(TimeOfDayPreset time) => time switch
    {
        TimeOfDayPreset.Dawn => "Рассвет",
        TimeOfDayPreset.Day => "День",
        TimeOfDayPreset.Sunset => "Закат",
        TimeOfDayPreset.Night => "Ночь",
        _ => "Авто"
    };

    private void SetupOrbitInput(Image surface)
    {
        EventTrigger trigger = surface.gameObject.AddComponent<EventTrigger>();
        EventTrigger.Entry down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        down.callback.AddListener(data =>
        {
            if (phase != Phase.Selecting) return;
            PointerEventData pointer = (PointerEventData)data;
            dragging = true;
            pointerPosition = pointer.position;
        });
        trigger.triggers.Add(down);
        EventTrigger.Entry drag = new EventTrigger.Entry { eventID = EventTriggerType.Drag };
        drag.callback.AddListener(data =>
        {
            PointerEventData pointer = (PointerEventData)data;
            if (!dragging || phase != Phase.Selecting) return;
            Vector2 delta = pointer.position - pointerPosition;
            pointerPosition = pointer.position;
            yaw = Mathf.Clamp(yaw - delta.x * 0.006f, -1.2f, 1.2f);
            cameraHeight = Mathf.Clamp(cameraHeight + delta.y * 0.008f, 1.45f, 4.6f);
            UpdateShowroomCamera(0.016f);
        });
        trigger.triggers.Add(drag);
        EventTrigger.Entry up = new EventTrigger.Entry { eventID = EventTriggerType.EndDrag };
        up.callback.AddListener(_ => dragging = false);
        trigger.triggers.Add(up);
        EventTrigger.Entry scroll = new EventTrigger.Entry { eventID = EventTriggerType.Scroll };
        scroll.callback.AddListener(data =>
        {
            if (phase != Phase.Selecting) return;
            PointerEventData pointer = (PointerEventData)data;
            cameraDistance = Mathf.Clamp(cameraDistance + pointer.scrollDelta.y * 0.008f, 5.3f, 10.5f);
            UpdateShowroomCamera(0.016f);
        });
        trigger.triggers.Add(scroll);
    }

    private void Update()
    {
        if (Keyboard.current == null) return;
        if (phase == Phase.Selecting && Keyboard.current.enterKey.wasPressedThisFrame)
            BeginStart();
        else if (phase == Phase.Selecting && Keyboard.current.escapeKey.wasPressedThisFrame)
            BackToGarage();
    }

    private void LateUpdate()
    {
        if (stageCamera == null) return;
        if (phase == Phase.Selecting)
        {
            if (!dragging)
            {
                yaw += Time.unscaledDeltaTime * 0.045f * orbitDirection;
                if (yaw >= 1.15f || yaw <= -1.15f)
                {
                    yaw = Mathf.Clamp(yaw, -1.15f, 1.15f);
                    orbitDirection *= -1f;
                }
            }
            UpdateShowroomCamera(Time.unscaledDeltaTime);
        }
        if (showcaseVehicle != null)
        {
            float targetRpm = phase == Phase.Selecting ? 650f : 900f + Mathf.Sin(Time.unscaledTime * 5f) * 180f;
            showcaseVehicle.rpm = Mathf.Lerp(showcaseVehicle.rpm, targetRpm, Time.unscaledDeltaTime * 3f);
        }
        if (phase == Phase.Loading && loadOperation != null)
        {
            float progress = Mathf.Clamp01(loadOperation.progress / 0.9f);
            loadingFill.rectTransform.anchorMax = new Vector2(Mathf.Lerp(0.25f, 0.75f, progress), 0.425f);
            loadingText.text = $"Подготовка трассы…  {Mathf.RoundToInt(progress * 100f)}%";
        }
    }

    private void UpdateShowroomCamera(float deltaTime, bool immediate = false)
    {
        if (stageCamera == null) return;
        Vector3 target = new Vector3(-0.55f, 0.9f, 0.35f);
        Vector3 position = target + new Vector3(Mathf.Sin(yaw) * cameraDistance, cameraHeight, Mathf.Cos(yaw) * cameraDistance);
        if (immediate)
            stageCamera.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position, Vector3.up));
        else
        {
            stageCamera.transform.position = Vector3.SmoothDamp(stageCamera.transform.position, position, ref cameraVelocity, 0.16f, Mathf.Max(0.001f, deltaTime));
            Quaternion rotation = Quaternion.LookRotation(target - stageCamera.transform.position, Vector3.up);
            stageCamera.transform.rotation = Quaternion.Slerp(stageCamera.transform.rotation, rotation, 1f - Mathf.Exp(-8f * deltaTime));
        }
    }

    private void BeginStart()
    {
        if (phase != Phase.Selecting) return;
        StartCoroutine(StartSequence());
    }

    private IEnumerator StartSequence()
    {
        // PreRace owns only the cinematic and loading screen. RaceManager owns the
        // expedition story and the single visible countdown in the race scene.
        phase = Phase.Loading;
        orbitSurface.raycastTarget = false;
        selectionPanel.SetActive(false);
        Vector3 startPosition = stageCamera.transform.position;
        Quaternion startRotation = stageCamera.transform.rotation;
        Vector3 endPosition = new Vector3(-4.9f, 2.45f, -6.9f);
        Quaternion endRotation = Quaternion.LookRotation(new Vector3(0f, 1f, 3f) - endPosition, Vector3.up);
        float duration = 1.8f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            stageCamera.transform.position = Vector3.Lerp(startPosition, endPosition, t);
            stageCamera.transform.rotation = Quaternion.Slerp(startRotation, endRotation, t);
            yield return null;
        }

        loadingPanel.SetActive(true);
        loadingText.text = $"{RaceTypeInfo.DisplayName(GameManager.Instance.SelectedRaceType)}  ·  {track.displayName}";
        GameManager.Instance.SkipRaceStory = false;
        loadOperation = SceneManager.LoadSceneAsync(RaceSceneName());
        loadOperation.allowSceneActivation = false;
        while (loadOperation.progress < 0.9f)
            yield return null;
        loadOperation.allowSceneActivation = true;
        yield return loadOperation;
    }

    private void BackToGarage()
    {
        if (phase != Phase.Selecting) return;
        Time.timeScale = 1f;
        SceneManager.LoadScene("Garage");
    }

    private string RaceSceneName()
    {
        return TrackData.SceneNameForTrack(track);
    }

    private static string FormatTime(float seconds)
    {
        int minutes = Mathf.FloorToInt(seconds / 60f);
        int secs = Mathf.FloorToInt(seconds % 60f);
        return $"{minutes:00}:{secs:00}";
    }
}
