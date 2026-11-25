using UnityEngine;

public class EngineAudio1912 : MonoBehaviour
{
    public VehicleController1912 vehicle;
    public AudioSource roadSource;
    public AudioSource windSource;
    public AudioSource starterSource;

    private AudioSource[] engineSources;
    private readonly string[] clips =
    {
        "engine_synthetic_4cyl_600rpm",
        "engine_synthetic_4cyl_900rpm",
        "engine_synthetic_4cyl_1200rpm",
        "engine_synthetic_4cyl_1500rpm",
        "engine_synthetic_4cyl_1800rpm",
        "engine_synthetic_4cyl_2100rpm"
    };

    private void Awake()
    {
        engineSources = new AudioSource[clips.Length];
        for (int i = 0; i < clips.Length; i++)
        {
            AudioClip clip = Resources.Load<AudioClip>($"Audio/{clips[i]}");
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.Play();
            engineSources[i] = source;
        }

        roadSource = CreateLoop("road_gravel_loop", 0f);
        windSource = CreateLoop("wind_open_cockpit_loop", 0f);
        starterSource = CreateOneShot("starter_hand_crank");
    }

    private AudioSource CreateLoop(string resource, float volume)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.clip = Resources.Load<AudioClip>($"Audio/{resource}");
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = volume;
        source.Play();
        return source;
    }

    private AudioSource CreateOneShot(string resource)
    {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.clip = Resources.Load<AudioClip>($"Audio/{resource}");
        source.spatialBlend = 0f;
        source.playOnAwake = false;
        return source;
    }

    public void PlayStarter()
    {
        if (starterSource != null)
            starterSource.Play();
    }

    private void Update()
    {
        if (vehicle == null || engineSources == null)
            return;

        float normalized = Mathf.InverseLerp(600f, 2100f, vehicle.rpm);
        float scaled = normalized * (engineSources.Length - 1);
        int lower = Mathf.Clamp(Mathf.FloorToInt(scaled), 0, engineSources.Length - 1);
        int upper = Mathf.Clamp(lower + 1, 0, engineSources.Length - 1);
        float blend = scaled - lower;
        float engineVolume = GameManager.Instance != null ? GameManager.Instance.Settings.engineVolume : 0.85f;
        for (int i = 0; i < engineSources.Length; i++)
        {
            float target = vehicle.engineRunning ? (i == lower ? 1f - blend : i == upper ? blend : 0f) * engineVolume : 0f;
            engineSources[i].volume = Mathf.Lerp(engineSources[i].volume, target, Time.deltaTime * 5f);
            engineSources[i].pitch = Mathf.Clamp(0.88f + normalized * 0.22f + vehicle.Throttle * 0.06f, 0.8f, 1.25f);
        }

        if (roadSource != null)
        {
            float speed = Mathf.Clamp01(vehicle.SpeedKmh / 90f);
            roadSource.volume = speed * 0.42f;
            roadSource.pitch = 0.75f + speed * 0.45f;
        }
        if (windSource != null)
        {
            float speed = Mathf.Clamp01(vehicle.SpeedKmh / 110f);
            windSource.volume = speed * speed * 0.55f;
            windSource.pitch = 0.8f + speed * 0.35f;
        }
    }
}
