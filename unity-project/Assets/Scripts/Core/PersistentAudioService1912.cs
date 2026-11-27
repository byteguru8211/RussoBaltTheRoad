using System;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-500)]
public class PersistentAudioService1912 : MonoBehaviour
{
    public static PersistentAudioService1912 Instance { get; private set; }

    private SettingsData fallbackSettings;
    private AudioSource menuMusicSource;
    private AudioSource menuAmbienceSource;
    private AudioSource raceAmbienceSource;
    private AudioSource clickSource;
    private float contextRefreshTimer;
    private float engineAudioElapsed;
    private float lastMasterVolume = -1f;
    private bool inRace;
    private bool paused;
    private bool results;

    public bool IsVehicleAudioAudible => !paused && !results;
    public bool IsRaceAudioAudible => inRace && IsVehicleAudioAudible;
    public bool IsInRace => inRace;
    public bool IsPaused => paused;
    public bool IsResults => results;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null)
            return;
        GameObject serviceObject = new GameObject("PersistentAudioService1912");
        serviceObject.AddComponent<PersistentAudioService1912>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        fallbackSettings = SaveSystem.LoadSettings();

        menuMusicSource = CreateSource("MenuMusic_1912", "Audio/menu_1912_original", true, 0f);
        menuAmbienceSource = CreateSource("MenuAmbience", "Audio/ambience_birds_loop", true, 0f);
        raceAmbienceSource = CreateSource("RaceAmbience", "Audio/ambience_crowd_loop", true, 0f);
        clickSource = CreateSource("UIClick", "Audio/gear_lever_clack", false, 0f);
        SceneManager.sceneLoaded += OnSceneLoaded;
        RefreshContext();
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this)
            Instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        RefreshContext();
    }

    private void Update()
    {
        float deltaTime = Mathf.Max(0.0001f, Time.unscaledDeltaTime);
        contextRefreshTimer -= deltaTime;
        if (contextRefreshTimer <= 0f)
        {
            RefreshContext();
            contextRefreshTimer = 0.1f;
        }

        SettingsData settings = CurrentSettings;
        if (!Mathf.Approximately(lastMasterVolume, settings.masterVolume))
        {
            AudioListener.volume = Mathf.Clamp01(settings.masterVolume);
            lastMasterVolume = settings.masterVolume;
        }

        float musicTarget = inRace || paused || results ? 0f : Mathf.Clamp01(settings.musicVolume) * 0.62f;
        float menuAmbienceTarget = inRace || results ? 0f : Mathf.Clamp01(settings.ambientVolume) * 0.2f;
        float raceAmbienceTarget = IsRaceAudioAudible ? Mathf.Clamp01(settings.ambientVolume) * 0.34f : 0f;
        Fade(menuMusicSource, musicTarget, deltaTime, 0.32f);
        Fade(menuAmbienceSource, menuAmbienceTarget, deltaTime, 0.45f);
        Fade(raceAmbienceSource, raceAmbienceTarget, deltaTime, 1.4f);

        engineAudioElapsed += deltaTime;
        if (engineAudioElapsed >= 0.2f)
        {
            RefreshDisabledVehicleAudio(engineAudioElapsed);
            engineAudioElapsed %= 0.2f;
        }
    }

    private SettingsData CurrentSettings => GameManager.Instance != null && GameManager.Instance.Settings != null
        ? GameManager.Instance.Settings
        : fallbackSettings;

    private void RefreshContext()
    {
        RaceManager race = RaceManager.Instance;
        inRace = race != null || SceneManager.GetActiveScene().name.StartsWith("Race_", StringComparison.OrdinalIgnoreCase);
        bool pauseMenu = inRace && FindAnyObjectByType<PauseMenuUI>() != null;
        bool resultsMenu = inRace && FindAnyObjectByType<ResultsUI>() != null;
        paused = Time.timeScale <= 0.0001f || pauseMenu;
        results = resultsMenu || (race != null && race.State == RaceManager.RaceState.Finished);
    }

    private void RefreshDisabledVehicleAudio(float elapsedTime)
    {
        // Disabled remote-car audio still needs updates in menus, grids and races.
        float elapsed = Mathf.Max(0.0001f, elapsedTime);
        foreach (EngineAudio1912 audio in FindObjectsByType<EngineAudio1912>())
        {
            if (audio == null || !audio.gameObject.activeInHierarchy || audio.enabled)
                continue;
            audio.TickExternal(elapsed);
        }
    }

    private AudioSource CreateSource(string sourceName, string resource, bool loop, float spatialBlend)
    {
        GameObject sourceObject = new GameObject(sourceName);
        sourceObject.transform.SetParent(transform, false);
        AudioSource source = sourceObject.AddComponent<AudioSource>();
        source.clip = Resources.Load<AudioClip>(resource);
        source.loop = loop;
        source.playOnAwake = false;
        source.spatialBlend = spatialBlend;
        source.volume = 0f;
        source.ignoreListenerPause = true;
        source.dopplerLevel = 0f;
        if (source.clip == null)
        {
            Debug.LogWarning($"Audio resource '{resource}' is missing.", this);
            return source;
        }
        if (loop)
            source.Play();
        return source;
    }

    private static void Fade(AudioSource source, float target, float deltaTime, float unitsPerSecond)
    {
        if (source != null)
            source.volume = Mathf.MoveTowards(source.volume, target, unitsPerSecond * deltaTime);
    }

    public void PlayUIClick()
    {
        if (clickSource == null || clickSource.clip == null)
            return;
        clickSource.pitch = UnityEngine.Random.Range(0.96f, 1.04f);
        clickSource.PlayOneShot(clickSource.clip, 0.5f);
    }
}
