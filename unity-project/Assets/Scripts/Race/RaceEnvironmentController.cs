using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class RaceEnvironmentController : MonoBehaviour
{
    public TrackEnvironmentProfile profile;
    public TrackBuilder trackBuilder;
    public Vector3 center;
    public float worldRadius = 300f;
    public WeatherPreset Weather { get; private set; } = WeatherPreset.Auto;
    public TimeOfDayPreset TimeOfDay { get; private set; } = TimeOfDayPreset.Auto;
    public float Wetness { get; private set; }

    private Light sun;
    private Material skybox;
    private ParticleSystem precipitation;
    private ParticleSystem wheelSpray;
    private Material precipitationMaterial;
    private Material cloudMaterial;
    private Material sprayMaterial;
    private Texture2D generatedParticleTexture;
    private Texture2D generatedSprayTexture;
    private AudioSource precipitationAudio;
    private AudioClip generatedRainAudio;
    private GameObject nightSkyRoot;
    private WeatherPreset requestedWeather = WeatherPreset.Auto;
    private TimeOfDayPreset requestedTimeOfDay = TimeOfDayPreset.Auto;
    private readonly List<Transform> clouds = new List<Transform>();
    private readonly List<VehicleController1912> vehicles = new List<VehicleController1912>();
    private readonly List<Object> transientAssets = new List<Object>();
    private float vehicleRefreshTimer;
    private float settingsRefreshTimer;
    private float cullingRefreshTimer;
    private float elapsed;
    private int audioNoiseState = 1912;
    private float audioFilter;

    public void Configure(TrackEnvironmentProfile environment, TrackBuilder builder, Vector3 worldCenter, float radius)
    {
        profile = environment;
        trackBuilder = builder;
        center = worldCenter;
        worldRadius = radius;
        ReadRequestedPreset();
        ResolvePreset();
        ResolveAtmosphere();
        CreateClouds();
        CreatePrecipitation();
        CreateNightSky();
        Wetness = Weather == WeatherPreset.Rain ? 0.65f : Weather == WeatherPreset.Snow ? 0.12f : 0f;
        ApplyAtmosphere();
        RefreshVehicles();
    }

    public void Reconfigure(WeatherPreset weather, TimeOfDayPreset timeOfDay)
    {
        requestedWeather = weather;
        requestedTimeOfDay = timeOfDay;
        ResolvePreset();
        ResolveAtmosphere();
        ClearWeatherVisuals();
        CreateClouds();
        CreatePrecipitation();
        CreateNightSky();
        Wetness = Weather == WeatherPreset.Rain ? 0.6f : Weather == WeatherPreset.Snow ? 0.12f : 0f;
        ApplyAtmosphere();
        RefreshVehicles();
    }

    private void ReadRequestedPreset()
    {
        SettingsData settings = GameManager.Instance != null ? GameManager.Instance.Settings : null;
        requestedWeather = settings != null ? settings.weather : WeatherPreset.Auto;
        requestedTimeOfDay = settings != null ? settings.timeOfDay : TimeOfDayPreset.Auto;
    }

    private void ResolvePreset()
    {
        string trackId = trackBuilder != null && trackBuilder.track != null ? trackBuilder.track.id : profile != null ? profile.id : "winter_departure";
        Weather = requestedWeather;
        TimeOfDay = requestedTimeOfDay;
        if (Weather == WeatherPreset.Auto)
        {
            Weather = trackId switch
            {
                "winter_departure" or "moscow_highway" => WeatherPreset.Snow,
                "finland_coast" or "alps_pass" => WeatherPreset.Fog,
                "tver_forest" => WeatherPreset.Rain,
                "riga_speedway" => WeatherPreset.Cloudy,
                _ => WeatherPreset.Clear
            };
        }
        if (TimeOfDay == TimeOfDayPreset.Auto)
        {
            TimeOfDay = trackId switch
            {
                "moscow_highway" => TimeOfDayPreset.Dawn,
                "monaco_finish" => TimeOfDayPreset.Sunset,
                "finland_coast" => TimeOfDayPreset.Dawn,
                "crimean_coast" => TimeOfDayPreset.Sunset,
                _ => TimeOfDayPreset.Day
            };
        }
    }

    private void ResolveAtmosphere()
    {
        foreach (Light light in FindObjectsByType<Light>(FindObjectsInactive.Include))
        {
            if (light.type != LightType.Directional) continue;
            if (sun == null || light.name == "Directional Light") sun = light;
        }
        skybox = RenderSettings.skybox;
    }

    private void CreateClouds()
    {
        GameObject[] prefabs =
        {
            Resources.Load<GameObject>("Environment/ReleaseKit/Cloud_Cumulus_01"),
            Resources.Load<GameObject>("Environment/ReleaseKit/Cloud_Cumulus_02"),
            Resources.Load<GameObject>("Environment/ReleaseKit/Cloud_Cumulus_03")
        };
        if (prefabs[0] == null) return;
        cloudMaterial = RaceMaterialFactory.CreateLit("RaceCloudSoft", new Color(0.93f, 0.95f, 0.98f, 0.78f), 1f);
        RaceMaterialFactory.MakeTransparent(cloudMaterial, new Color(0.93f, 0.95f, 0.98f, 0.78f));
        transientAssets.Add(cloudMaterial);
        int count = Weather == WeatherPreset.Clear ? 4 : Weather == WeatherPreset.Fog ? 7 : Weather == WeatherPreset.Cloudy ? 6 : 9;
        for (int i = 0; i < count; i++)
        {
            GameObject prefab = prefabs[i % prefabs.Length];
            if (prefab == null) continue;
            float angle = i * Mathf.PI * 2f / count + 0.37f;
            float radius = worldRadius * Mathf.Lerp(0.58f, 0.92f, (i % 5) / 4f);
            Vector3 position = center + new Vector3(Mathf.Cos(angle) * radius, 95f + (i % 4) * 18f, Mathf.Sin(angle) * radius);
            GameObject cloud = TrackSceneryBuilder.InstantiateAsset(prefab, position, Quaternion.Euler(0f, i * 47f, 0f), transform);
            float scale = Mathf.Lerp(5.5f, 9f, (i % 3) / 2f);
            cloud.transform.localScale = prefab.transform.localScale * scale;
            cloud.name = "Cloud_" + i.ToString("00");
            foreach (MeshRenderer renderer in cloud.GetComponentsInChildren<MeshRenderer>())
            {
                renderer.sharedMaterial = cloudMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            clouds.Add(cloud.transform);
        }
        UpdateCloudAppearance();
    }

    private void CreatePrecipitation()
    {
        if (Weather != WeatherPreset.Rain && Weather != WeatherPreset.Snow) return;
        bool rain = Weather == WeatherPreset.Rain;
        Texture2D texture = Resources.Load<Texture2D>("Textures/particle_smoke");
        if (texture == null)
        {
            generatedParticleTexture = CreateParticleTexture(rain);
            texture = generatedParticleTexture;
            transientAssets.Add(generatedParticleTexture);
        }
        precipitationMaterial = RaceMaterialFactory.CreateParticle("RacePrecipitation", Color.white, texture);
        transientAssets.Add(precipitationMaterial);
        GameObject systemObject = new GameObject("Precipitation");
        systemObject.transform.SetParent(transform, false);
        precipitation = systemObject.AddComponent<ParticleSystem>();
        precipitation.Stop();
        ParticleSystem.MainModule main = precipitation.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = rain ? 900 : 650;
        main.startLifetime = rain ? new ParticleSystem.MinMaxCurve(2.8f, 4.2f) : new ParticleSystem.MinMaxCurve(6f, 9f);
        main.startSpeed = rain ? new ParticleSystem.MinMaxCurve(2f, 4f) : new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        main.startSize = rain ? new ParticleSystem.MinMaxCurve(0.035f, 0.055f) : new ParticleSystem.MinMaxCurve(0.035f, 0.075f);
        main.startColor = rain ? new Color(0.66f, 0.76f, 0.82f, 0.48f) : new Color(0.96f, 0.98f, 1f, 0.76f);
        main.gravityModifier = 1f;
        main.startSize3D = true;
        main.startSizeX = rain ? new ParticleSystem.MinMaxCurve(0.012f) : new ParticleSystem.MinMaxCurve(0.035f, 0.075f);
        main.startSizeY = rain ? new ParticleSystem.MinMaxCurve(0.42f) : new ParticleSystem.MinMaxCurve(0.035f, 0.075f);
        main.startSizeZ = rain ? new ParticleSystem.MinMaxCurve(0.012f) : new ParticleSystem.MinMaxCurve(0.035f, 0.075f);
        ParticleSystem.ShapeModule shape = precipitation.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.rotation = new Vector3(180f, 0f, 0f);
        shape.position = Vector3.zero;
        shape.radius = 19f;
        shape.angle = 9f;
        shape.length = 0.35f;
        ParticleSystem.EmissionModule emission = precipitation.emission;
        emission.rateOverTime = rain ? 260f : 150f;
        ParticleSystem.NoiseModule noise = precipitation.noise;
        noise.enabled = true;
        noise.strength = rain ? 0.06f : 0.28f;
        noise.frequency = rain ? 0.8f : 0.28f;
        ParticleSystem.CollisionModule collision = precipitation.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.quality = ParticleSystemCollisionQuality.High;
        collision.lifetimeLoss = rain ? 0.32f : 0.12f;
        collision.dampen = 0.18f;
        collision.bounce = 0f;
        ParticleSystemRenderer renderer = precipitation.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = precipitationMaterial;
        renderer.renderMode = rain ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.velocityScale = 0.1f;
        renderer.lengthScale = 1.35f;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.maxParticleSize = 0.6f;
        if (rain)
        {
            CreateRainAudio(systemObject);
            CreateWheelSpray();
        }
        UpdatePrecipitationPosition();
        precipitation.Play();
    }

    private void CreateRainAudio(GameObject parent)
    {
        const int sampleRate = 22050;
        generatedRainAudio = Resources.Load<AudioClip>("Audio/rain_soft_loop");
        if (generatedRainAudio == null)
        {
            generatedRainAudio = AudioClip.Create("RaceRainAmbience", sampleRate * 4, 1, sampleRate, false, ReadRainAudio);
            transientAssets.Add(generatedRainAudio);
        }
        precipitationAudio = parent.AddComponent<AudioSource>();
        precipitationAudio.clip = generatedRainAudio;
        precipitationAudio.loop = true;
        precipitationAudio.spatialBlend = 0f;
        precipitationAudio.dopplerLevel = 0f;
        precipitationAudio.volume = 0f;
        precipitationAudio.Play();
    }

    private void CreateWheelSpray()
    {
        Texture2D texture = Resources.Load<Texture2D>("Textures/particle_smoke");
        if (texture == null)
        {
            generatedSprayTexture = CreateParticleTexture(false);
            texture = generatedSprayTexture;
            transientAssets.Add(generatedSprayTexture);
        }
        sprayMaterial = RaceMaterialFactory.CreateParticle("RaceWheelSpray", new Color(0.62f, 0.7f, 0.74f, 0.32f), texture);
        transientAssets.Add(sprayMaterial);
        GameObject systemObject = new GameObject("WheelSpray");
        systemObject.transform.SetParent(transform, false);
        wheelSpray = systemObject.AddComponent<ParticleSystem>();
        wheelSpray.Stop();
        ParticleSystem.MainModule main = wheelSpray.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 140;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.65f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.4f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.14f);
        main.startColor = new Color(0.62f, 0.7f, 0.74f, 0.3f);
        main.gravityModifier = 1f;
        ParticleSystem.ShapeModule shape = wheelSpray.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.rotation = new Vector3(-90f, 0f, 0f);
        shape.radius = 0.72f;
        shape.angle = 24f;
        shape.length = 0.12f;
        ParticleSystem.EmissionModule emission = wheelSpray.emission;
        emission.rateOverTime = 0f;
        ParticleSystem.CollisionModule collision = wheelSpray.collision;
        collision.enabled = true;
        collision.type = ParticleSystemCollisionType.World;
        collision.mode = ParticleSystemCollisionMode.Collision3D;
        collision.quality = ParticleSystemCollisionQuality.Medium;
        collision.lifetimeLoss = 0.25f;
        collision.dampen = 0.35f;
        collision.bounce = 0f;
        ParticleSystemRenderer renderer = wheelSpray.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = sprayMaterial;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.maxParticleSize = 0.4f;
        wheelSpray.Play();
    }

    private void CreateNightSky()
    {
        if (TimeOfDay != TimeOfDayPreset.Night) return;
        nightSkyRoot = new GameObject("NightSky");
        nightSkyRoot.transform.SetParent(transform, false);
        nightSkyRoot.transform.position = new Vector3(center.x, 0f, center.z);
        Material starMaterial = RaceMaterialFactory.CreateUnlit("RaceStars", new Color(0.78f, 0.86f, 1f));
        starMaterial.SetFloat("_Cull", 0f);
        transientAssets.Add(starMaterial);
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();
        System.Random random = new System.Random(241912);
        for (int i = 0; i < 150; i++)
        {
            float azimuth = (float)random.NextDouble() * Mathf.PI * 2f;
            float elevation = 0.18f + (float)random.NextDouble() * 0.78f;
            Vector3 direction = new Vector3(Mathf.Cos(azimuth) * Mathf.Cos(elevation), Mathf.Sin(elevation), Mathf.Sin(azimuth) * Mathf.Cos(elevation));
            Vector3 right = Vector3.Cross(direction, Vector3.up).normalized;
            if (right.sqrMagnitude < 0.001f) right = Vector3.right;
            Vector3 up = Vector3.Cross(right, direction).normalized;
            Quaternion rotation = Quaternion.LookRotation(-direction, up);
            float size = TrackMeshBuilder.RandomRange(random, 0.65f, 1.5f);
            Vector3 starRight = rotation * Vector3.right * size;
            Vector3 starUp = rotation * Vector3.up * size;
            Vector3 star = direction * 360f;
            TrackMeshBuilder.AddQuad(vertices, uvs, triangles, star - starRight - starUp, star + starRight - starUp,
                star + starRight + starUp, star - starRight + starUp);
        }
        Mesh stars = TrackMeshBuilder.Create("RaceStars", vertices, uvs, new[] { triangles }, new[] { starMaterial });
        transientAssets.Add(stars);
        TrackMeshBuilder.CreateObject("Stars", stars, new[] { starMaterial }, nightSkyRoot.transform, false, false);

        Material moonMaterial = RaceMaterialFactory.CreateUnlit("RaceMoon", new Color(0.82f, 0.87f, 0.96f));
        transientAssets.Add(moonMaterial);
        GameObject moon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        moon.name = "Moon";
        moon.transform.SetParent(nightSkyRoot.transform, false);
        moon.transform.localPosition = new Vector3(-0.45f, 0.62f, -0.64f).normalized * 330f;
        moon.transform.localScale = Vector3.one * 18f;
        moon.GetComponent<MeshRenderer>().sharedMaterial = moonMaterial;
        moon.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        Destroy(moon.GetComponent<Collider>());

        Material haloMaterial = RaceMaterialFactory.CreateLit("RaceMoonHalo", new Color(0.52f, 0.64f, 0.84f, 0.07f), 0f);
        RaceMaterialFactory.MakeTransparent(haloMaterial, new Color(0.52f, 0.64f, 0.84f, 0.07f));
        transientAssets.Add(haloMaterial);
        GameObject halo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        halo.name = "MoonHalo";
        halo.transform.SetParent(nightSkyRoot.transform, false);
        halo.transform.localPosition = moon.transform.localPosition;
        halo.transform.localScale = Vector3.one * 25f;
        halo.GetComponent<MeshRenderer>().sharedMaterial = haloMaterial;
        halo.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
        Destroy(halo.GetComponent<Collider>());
    }

    private void ApplyAtmosphere()
    {
        Color fog = profile.fogColor;
        Color sky = profile.skyTint;
        Color ground = profile.skyGroundColor;
        Color sunColor = profile.sunColor;
        float sunIntensity = profile.sunIntensity;
        float exposure = profile.exposure;
        Vector3 sunRotation = new Vector3(48f, -32f, 0f);
        switch (TimeOfDay)
        {
            case TimeOfDayPreset.Dawn:
                fog = Color.Lerp(fog, new Color(0.6f, 0.56f, 0.59f), 0.5f);
                sky = Color.Lerp(sky, new Color(0.46f, 0.5f, 0.61f), 0.44f);
                sunColor = new Color(1f, 0.72f, 0.5f);
                sunIntensity *= 0.7f;
                sunRotation = new Vector3(24f, -72f, 0f);
                exposure += 0.04f;
                break;
            case TimeOfDayPreset.Sunset:
                fog = Color.Lerp(fog, new Color(0.74f, 0.42f, 0.27f), 0.52f);
                sky = Color.Lerp(sky, new Color(0.34f, 0.3f, 0.43f), 0.44f);
                sunColor = new Color(1f, 0.46f, 0.2f);
                sunIntensity *= 0.76f;
                sunRotation = new Vector3(18f, 68f, 0f);
                exposure += 0.08f;
                break;
            case TimeOfDayPreset.Night:
                fog = Color.Lerp(fog, new Color(0.035f, 0.055f, 0.09f), 0.88f);
                sky = Color.Lerp(sky, new Color(0.025f, 0.04f, 0.08f), 0.92f);
                ground = Color.Lerp(ground, new Color(0.02f, 0.03f, 0.05f), 0.9f);
                sunColor = new Color(0.42f, 0.58f, 0.82f);
                sunIntensity = 0.16f;
                exposure -= 0.18f;
                sunRotation = new Vector3(58f, 142f, 0f);
                break;
        }
        float fogFactor = Weather switch
        {
            WeatherPreset.Fog => 3.4f,
            WeatherPreset.Rain => 1.8f,
            WeatherPreset.Snow => 1.65f,
            WeatherPreset.Cloudy => 1.25f,
            _ => 1f
        };
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = Mathf.Clamp(profile.fogDensity * fogFactor, 0.0015f, 0.022f);
        RenderSettings.fogColor = fog;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientSkyColor = Color.Lerp(sky, Color.white, 0.12f) * (TimeOfDay == TimeOfDayPreset.Night ? 0.35f : 1f);
        RenderSettings.ambientEquatorColor = fog * 0.72f;
        RenderSettings.ambientGroundColor = ground * 0.5f;
        if (skybox != null)
        {
            skybox.SetColor("_SkyTint", sky);
            skybox.SetColor("_GroundColor", ground);
            skybox.SetFloat("_AtmosphereThickness", profile.atmosphereThickness);
            skybox.SetFloat("_Exposure", exposure);
            skybox.SetVector("_SunDirection", Quaternion.Euler(sunRotation) * Vector3.forward);
        }
        if (sun != null)
        {
            sun.color = sunColor;
            sun.intensity = sunIntensity;
            sun.transform.rotation = Quaternion.Euler(sunRotation);
        }
        UpdateCloudAppearance();
        if (nightSkyRoot != null) nightSkyRoot.SetActive(TimeOfDay == TimeOfDayPreset.Night);
        trackBuilder?.ApplyWeather(Weather, Wetness);
    }

    private void UpdateCloudAppearance()
    {
        if (cloudMaterial == null) return;
        float alpha = Weather switch
        {
            WeatherPreset.Clear => 0.68f,
            WeatherPreset.Fog => 0.25f,
            WeatherPreset.Rain => 0.6f,
            WeatherPreset.Snow => 0.66f,
            WeatherPreset.Cloudy => 0.8f,
            _ => 0.72f
        };
        if (TimeOfDay == TimeOfDayPreset.Night) alpha *= 0.42f;
        Color color = TimeOfDay == TimeOfDayPreset.Night ? new Color(0.3f, 0.36f, 0.48f, alpha)
            : TimeOfDay == TimeOfDayPreset.Sunset ? new Color(0.95f, 0.72f, 0.58f, alpha)
            : new Color(0.93f, 0.95f, 0.98f, alpha);
        cloudMaterial.SetColor("_BaseColor", color);
    }

    private void RefreshVehicles()
    {
        vehicles.Clear();
        vehicles.AddRange(FindObjectsByType<VehicleController1912>(FindObjectsInactive.Include));
        float grip = trackBuilder != null && trackBuilder.track != null ? trackBuilder.track.surfaceGrip : 1f;
        if (Weather == WeatherPreset.Rain) grip *= 0.82f;
        if (Weather == WeatherPreset.Snow) grip *= 0.86f;
        foreach (VehicleController1912 vehicle in vehicles)
        {
            if (vehicle == null) continue;
            vehicle.SetSurfaceGrip(grip);
            VehicleEffects1912 effects = vehicle.GetComponent<VehicleEffects1912>();
            if (effects == null) continue;
            effects.automaticHeadlamps = true;
            effects.headlampIntensity = TimeOfDay == TimeOfDayPreset.Night ? 4.2f : 3.2f;
        }
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        settingsRefreshTimer -= Time.deltaTime;
        if (settingsRefreshTimer <= 0f)
        {
            settingsRefreshTimer = 0.25f;
            SettingsData settings = GameManager.Instance != null ? GameManager.Instance.Settings : null;
            if (settings != null && (settings.weather != requestedWeather || settings.timeOfDay != requestedTimeOfDay))
                Reconfigure(settings.weather, settings.timeOfDay);
        }

        float targetWetness = Weather == WeatherPreset.Rain ? 1f : Weather == WeatherPreset.Snow ? 0.18f : 0f;
        float previousWetness = Wetness;
        Wetness = Mathf.MoveTowards(Wetness, targetWetness, Time.deltaTime * 0.22f);
        if (Mathf.Abs(previousWetness - Wetness) > 0.0001f) trackBuilder?.ApplyWeather(Weather, Wetness);

        float windSpeed = Weather == WeatherPreset.Rain ? 0.52f : Weather == WeatherPreset.Snow ? 0.16f : 0.24f;
        for (int i = 0; i < clouds.Count; i++)
        {
            Transform cloud = clouds[i];
            if (cloud == null) continue;
            Vector3 position = cloud.position;
            position.x += Time.deltaTime * windSpeed * (1f + i * 0.025f);
            position.z += Time.deltaTime * windSpeed * 0.18f;
            if (position.x > center.x + worldRadius) position.x = center.x - worldRadius;
            if (position.z > center.z + worldRadius) position.z = center.z - worldRadius;
            cloud.position = position;
        }

        VehicleController1912 player = RaceManager.Instance != null ? RaceManager.Instance.Player : null;
        Vector3 focus = player != null ? player.transform.position : trackBuilder != null ? trackBuilder.StartPoint : center;
        if (precipitation != null)
        {
            UpdatePrecipitationPosition();
            if (precipitationAudio != null)
            {
                precipitationAudio.volume = Mathf.Lerp(precipitationAudio.volume, Wetness * 0.18f, Time.deltaTime * 2f);
                precipitationAudio.pitch = 0.96f + Mathf.Sin(elapsed * 0.17f) * 0.025f;
            }
        }
        if (wheelSpray != null)
        {
            float speed = player != null ? Mathf.Abs(player.SpeedKmh) : 0f;
            ParticleSystem.EmissionModule sprayEmission = wheelSpray.emission;
            sprayEmission.rateOverTime = Weather == WeatherPreset.Rain && speed > 8f ? 18f + speed * 0.9f : 0f;
            if (player != null)
                wheelSpray.transform.SetPositionAndRotation(player.transform.position - player.transform.forward * 1.35f + Vector3.up * 0.22f, player.transform.rotation);
        }
        if (nightSkyRoot != null) nightSkyRoot.transform.position = new Vector3(focus.x, 0f, focus.z);

        cullingRefreshTimer -= Time.deltaTime;
        if (cullingRefreshTimer <= 0f)
        {
            cullingRefreshTimer = 0.45f;
            float cullDistance = profile != null && profile.sceneryCullDistance > 0f ? profile.sceneryCullDistance : 240f;
            TrackSceneryBuilder.UpdateTreeCulling(focus, cullDistance);
        }
        vehicleRefreshTimer -= Time.deltaTime;
        if (vehicleRefreshTimer <= 0f)
        {
            vehicleRefreshTimer = 1f;
            RefreshVehicles();
        }
    }

    private void UpdatePrecipitationPosition()
    {
        VehicleController1912 player = RaceManager.Instance != null ? RaceManager.Instance.Player : null;
        Vector3 focus = player != null ? player.transform.position : trackBuilder != null ? trackBuilder.StartPoint : center;
        float roadHeight = trackBuilder != null ? trackBuilder.RoadHeightAt(focus) : focus.y;
        precipitation.transform.SetPositionAndRotation(new Vector3(focus.x, roadHeight + 21f, focus.z), Quaternion.identity);
    }

    private void ReadRainAudio(float[] data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            audioNoiseState ^= audioNoiseState << 13;
            audioNoiseState ^= audioNoiseState >> 17;
            audioNoiseState ^= audioNoiseState << 5;
            float white = (audioNoiseState & 0xffff) / 32768f - 1f;
            audioFilter += (white - audioFilter) * 0.07f;
            data[i] = Mathf.Clamp(audioFilter * 0.72f + white * 0.035f, -1f, 1f);
        }
    }

    private void ClearWeatherVisuals()
    {
        foreach (Transform cloud in clouds) if (cloud != null) Destroy(cloud.gameObject);
        clouds.Clear();
        if (precipitation != null) Destroy(precipitation.gameObject);
        if (wheelSpray != null) Destroy(wheelSpray.gameObject);
        if (nightSkyRoot != null) Destroy(nightSkyRoot.gameObject);
        precipitation = null;
        wheelSpray = null;
        precipitationAudio = null;
        nightSkyRoot = null;
        precipitationMaterial = null;
        cloudMaterial = null;
        sprayMaterial = null;
        generatedParticleTexture = null;
        generatedSprayTexture = null;
        generatedRainAudio = null;
        foreach (Object asset in transientAssets) if (asset != null) Destroy(asset);
        transientAssets.Clear();
    }

    private static Texture2D CreateParticleTexture(bool rain)
    {
        Texture2D texture = new Texture2D(rain ? 8 : 32, rain ? 32 : 32, TextureFormat.RGBA32, true, true);
        Color[] pixels = new Color[texture.width * texture.height];
        for (int y = 0; y < texture.height; y++)
        {
            for (int x = 0; x < texture.width; x++)
            {
                float nx = (x + 0.5f) / texture.width * 2f - 1f;
                float ny = (y + 0.5f) / texture.height * 2f - 1f;
                float alpha = rain ? Mathf.Clamp01(1f - Mathf.Abs(nx) * 5f) * Mathf.Clamp01(1f - Mathf.Abs(ny) * 0.2f) : Mathf.Clamp01(1f - Mathf.Sqrt(nx * nx + ny * ny));
                pixels[y * texture.width + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(true, true);
        return texture;
    }

    private void OnDestroy()
    {
        ClearWeatherVisuals();
    }
}
