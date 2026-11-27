using UnityEngine;

[DisallowMultipleComponent]
public class EngineAudio1912 : MonoBehaviour
{
    public VehicleController1912 vehicle;
    public AudioSource roadSource;
    public AudioSource windSource;
    public AudioSource starterSource;
    public AudioSource gearSource;
    public AudioSource hornSource;

    private readonly string[] clips =
    {
        "engine_synthetic_4cyl_600rpm",
        "engine_synthetic_4cyl_900rpm",
        "engine_synthetic_4cyl_1200rpm",
        "engine_synthetic_4cyl_1500rpm",
        "engine_synthetic_4cyl_1800rpm",
        "engine_synthetic_4cyl_2100rpm"
    };

    private AudioSource[] engineSources;
    private bool initialized;
    private bool spatialized;

    public bool IsSpatialized => spatialized;

    private void Awake()
    {
        EnsureInitialized();
    }

    public void EnsureInitialized()
    {
        if (initialized)
            return;
        initialized = true;
        if (vehicle == null)
            vehicle = GetComponent<VehicleController1912>();

        engineSources = new AudioSource[clips.Length];
        for (int i = 0; i < clips.Length; i++)
        {
            engineSources[i] = CreateLoop(clips[i]);
            if (engineSources[i] != null)
                engineSources[i].volume = 0f;
        }

        roadSource = EnsureSource(roadSource, "road_gravel_loop", true);
        windSource = EnsureSource(windSource, "wind_open_cockpit_loop", true);
        starterSource = EnsureSource(starterSource, "starter_hand_crank", false);
        gearSource = EnsureSource(gearSource, "gear_lever_clack", false);
        hornSource = EnsureSource(hornSource, "horn_close_era", false);
        UpdateSpatialMode();
    }

    private AudioSource CreateLoop(string resource)
    {
        return EnsureSource(null, resource, true);
    }

    private AudioSource EnsureSource(AudioSource source, string resource, bool loop)
    {
        if (source == null)
            source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = 0f;
        source.minDistance = 3f;
        source.maxDistance = 45f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.dopplerLevel = 0f;
        if (source.clip == null)
            source.clip = Resources.Load<AudioClip>($"Audio/{resource}");
        if (source.clip == null)
        {
            // Keep the created source alive even if an optional resource is missing;
            // callers can still configure it and a later clip assignment can be used.
            Debug.LogWarning($"Engine audio resource 'Audio/{resource}' is missing.", this);
            source.volume = 0f;
            return source;
        }
        if (loop)
        {
            source.volume = 0f;
            if (!source.isPlaying) source.Play();
        }
        return source;
    }

    public void SetSpatialMode(bool spatial)
    {
        spatialized = spatial;
        ConfigureSpatialSource(roadSource);
        ConfigureSpatialSource(windSource);
        ConfigureSpatialSource(starterSource);
        ConfigureSpatialSource(gearSource);
        ConfigureSpatialSource(hornSource);
        if (engineSources != null)
        {
            foreach (AudioSource source in engineSources)
                ConfigureSpatialSource(source);
        }
    }

    private void ConfigureSpatialSource(AudioSource source)
    {
        if (source == null)
            return;
        source.spatialBlend = spatialized ? 1f : 0f;
        source.minDistance = 3f;
        source.maxDistance = 45f;
        source.rolloffMode = AudioRolloffMode.Logarithmic;
        source.dopplerLevel = spatialized ? 1f : 0f;
    }

    private void UpdateSpatialMode()
    {
        SetSpatialMode(vehicle != null && !vehicle.isPlayer);
    }

    public void PlayStarter()
    {
        if (starterSource != null && starterSource.clip != null)
            starterSource.PlayOneShot(starterSource.clip, OneShotVolume(0.9f));
    }

    public void PlayGearShift()
    {
        if (gearSource != null && gearSource.clip != null)
            gearSource.PlayOneShot(gearSource.clip, OneShotVolume(0.55f));
    }

    public void PlayHorn()
    {
        if (hornSource != null && hornSource.clip != null && !hornSource.isPlaying)
            hornSource.PlayOneShot(hornSource.clip, OneShotVolume(0.72f));
    }

    private float OneShotVolume(float volume)
    {
        SettingsData settings = GameManager.Instance != null ? GameManager.Instance.Settings : null;
        float engineVolume = settings != null ? settings.engineVolume : 0.85f;
        bool audible = PersistentAudioService1912.Instance == null || PersistentAudioService1912.Instance.IsVehicleAudioAudible;
        return Mathf.Clamp01(volume * engineVolume) * (audible ? 1f : 0f);
    }

    private void Update()
    {
        if (!initialized || engineSources == null)
            return;
        Tick(Time.unscaledDeltaTime, false);
    }

    // NetworkPlayerCar disables this component on remote clients. The persistent audio service
    // calls this method so those engine sources still follow the replicated transform spatially.
    public void TickExternal(float deltaTime)
    {
        EnsureInitialized();
        UpdateSpatialMode();
        Tick(deltaTime, true);
    }

    private void Tick(float deltaTime, bool external)
    {
        if (vehicle == null)
            return;
        UpdateSpatialMode();

        SettingsData settings = GameManager.Instance != null ? GameManager.Instance.Settings : null;
        float engineVolume = settings != null ? Mathf.Clamp01(settings.engineVolume) : 0.85f;
        float ambientVolume = settings != null ? Mathf.Clamp01(settings.ambientVolume) : 0.65f;
        bool serviceAllowsAudio = PersistentAudioService1912.Instance == null || PersistentAudioService1912.Instance.IsVehicleAudioAudible;
        float audible = serviceAllowsAudio ? 1f : 0f;
        float speed = Mathf.Clamp01(Mathf.Abs(vehicle.SpeedKmh) / 110f);
        float rpm = vehicle.rpm;
        bool engineAudible = vehicle.engineRunning;
        if (external)
        {
            float absoluteSpeed = Mathf.Abs(vehicle.SpeedKmh);
            rpm = Mathf.Max(rpm, 650f + absoluteSpeed * 16f);
            engineAudible = engineAudible || absoluteSpeed > 1f;
        }

        float normalized = Mathf.InverseLerp(600f, 2100f, Mathf.Clamp(rpm, 600f, 2100f));
        float scaled = normalized * (engineSources.Length - 1);
        int lower = Mathf.Clamp(Mathf.FloorToInt(scaled), 0, engineSources.Length - 1);
        int upper = Mathf.Clamp(lower + 1, 0, engineSources.Length - 1);
        float blend = scaled - lower;
        float smoothing = 1f - Mathf.Exp(-6f * Mathf.Max(0.0001f, deltaTime));

        for (int i = 0; i < engineSources.Length; i++)
        {
            AudioSource source = engineSources[i];
            if (source == null)
                continue;
            float weight = i == lower ? 1f - blend : i == upper ? blend : 0f;
            float targetVolume = engineAudible ? weight * engineVolume * audible : 0f;
            source.volume = Mathf.Lerp(source.volume, targetVolume, smoothing);
            if (engineAudible && source.clip != null && !source.isPlaying)
                source.Play();
            source.pitch = Mathf.Clamp(0.97f + normalized * 0.06f + vehicle.Throttle * 0.025f, 0.9f, 1.12f);
        }

        if (roadSource != null)
        {
            roadSource.volume = Mathf.Lerp(roadSource.volume, speed * 0.38f * ambientVolume * audible, smoothing);
            roadSource.pitch = 0.78f + speed * 0.42f;
        }
        if (windSource != null)
        {
            windSource.volume = Mathf.Lerp(windSource.volume, speed * speed * 0.5f * ambientVolume * audible, smoothing);
            windSource.pitch = 0.82f + speed * 0.32f;
        }

        float oneShotTarget = serviceAllowsAudio ? 1f : 0f;
        if (starterSource != null) starterSource.volume = Mathf.Lerp(starterSource.volume, oneShotTarget, smoothing);
        if (gearSource != null) gearSource.volume = Mathf.Lerp(gearSource.volume, oneShotTarget, smoothing);
        if (hornSource != null) hornSource.volume = Mathf.Lerp(hornSource.volume, oneShotTarget, smoothing);
    }
}
