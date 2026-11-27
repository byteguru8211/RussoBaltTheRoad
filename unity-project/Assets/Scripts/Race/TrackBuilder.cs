using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

public class TrackBuilder : MonoBehaviour
{
    public TrackData track;
    public int sampleCount = 220;
    public int checkpointCount = 12;
    public TrackEnvironmentProfile environmentProfile;

    public List<Vector3> Samples { get; private set; } = new List<Vector3>();
    public List<Vector3> Tangents { get; private set; } = new List<Vector3>();
    public TrackData.TrackSection[] EffectiveSections { get; private set; } = new TrackData.TrackSection[0];
    public float TrackLength { get; private set; }
    public Vector3 StartPoint => Samples.Count > 0 ? Samples[0] : Vector3.zero;
    public Vector3 StartTangent => Tangents.Count > 0 ? Tangents[0] : Vector3.forward;
    public const float RoadSurfaceOffset = 0.045f;
    public const float StartSafeZoneMeters = 25f;
    public float Wetness { get; private set; }

    private readonly List<Vector3> rights = new List<Vector3>();
    private readonly List<Vector3> normals = new List<Vector3>();
    private readonly List<float> distances = new List<float>();
    private readonly List<Object> generatedAssets = new List<Object>();
    private GameObject generatedRoot;
    private TrackEnvironmentProfile profile;
    private TrackRoute route;
    private TrackTerrainBuilder terrain;
    private System.Random random;
    private Material roadMaterial;
    private Material groundMaterial;
    private Material snowMaterial;
    private Material shoulderMaterial;
    private Material curbMaterial;
    private Material curbAlternateMaterial;
    private Material stoneMaterial;
    private Material woodMaterial;
    private Material metalMaterial;
    private Material bannerMaterial;
    private Material darkMaterial;
    private Material glassMaterial;
    private Material foliageMaterial;
    private Material rockMaterial;
    private Material warmStoneMaterial;
    private Material roofMaterial;
    private Material seaMaterial;
    private Material sailMaterial;
    private Material lampMaterial;
    private Material puddleMaterial;
    private TrackSurface1912 roadSurface;
    private TrackSurface1912 terrainSurface;
    private Color dryRoadTint;
    private Color dryShoulderTint;
    private float dryRoadSmoothness;
    private float dryShoulderSmoothness;
    private bool roadHasRoughnessMap;
    private bool shoulderHasRoughnessMap;
    private VolumeProfile volumeProfile;

    public void Build(TrackData data)
    {
        if (data == null) throw new System.ArgumentNullException(nameof(data));
        track = data;
        ClearGenerated();
        Samples.Clear();
        Tangents.Clear();
        rights.Clear();
        normals.Clear();
        distances.Clear();
        TrackLength = 0f;
        sampleCount = Mathf.Clamp(sampleCount, 32, 512);
        checkpointCount = Mathf.Clamp(checkpointCount, 2, 64);
        if (track.waypoints == null || track.waypoints.Length < 4) track.waypoints = DefaultLoop();
        EffectiveSections = track.GetSections();
        route = new TrackRoute(track);

        ResolveProfile();
        generatedRoot = new GameObject("Track_Generated");
        generatedRoot.transform.SetParent(transform, false);
        random = new System.Random(unchecked(profile.seed + StableId(track.id ?? string.Empty)));
        CreateMaterials();
        CreateSampledPath();
        CreateAtmosphere();
        CreateGround();
        CreateRoadMesh();
        CreateTrackSections();
        CreateCurbs();
        CreateBarriers();
        CreateStartGantry();
        CreateCheckpoints();
        CreateScenery();
        CreateRoadsideDetails();
        CreatePuddles();
        RaceEnvironmentController environment = generatedRoot.AddComponent<RaceEnvironmentController>();
        environment.Configure(profile, this, terrain.Center, terrain.Size * 0.48f);
        RegionalLandmarks.Build(track, this, generatedRoot.transform);
    }

    public void ApplyWeather(WeatherPreset weather, float wetness)
    {
        if (roadMaterial == null) return;
        Wetness = Mathf.Clamp01(wetness);
        float wetSmoothness = weather == WeatherPreset.Rain ? 0.78f : weather == WeatherPreset.Snow ? 0.2f : 0.22f;
        roadMaterial.SetColor("_BaseColor", Color.Lerp(dryRoadTint, dryRoadTint * new Color(0.46f, 0.53f, 0.61f), Wetness));
        roadMaterial.SetFloat("_Smoothness", Mathf.Lerp(dryRoadSmoothness, wetSmoothness, Wetness));
        SetRoughnessMapEnabled(roadMaterial, roadHasRoughnessMap, Wetness <= 0.015f);
        if (shoulderMaterial != null)
        {
            shoulderMaterial.SetColor("_BaseColor", Color.Lerp(dryShoulderTint, dryShoulderTint * new Color(0.58f, 0.64f, 0.68f), Wetness));
            shoulderMaterial.SetFloat("_Smoothness", Mathf.Lerp(dryShoulderSmoothness, Mathf.Min(0.58f, wetSmoothness + 0.04f), Wetness));
            SetRoughnessMapEnabled(shoulderMaterial, shoulderHasRoughnessMap, Wetness <= 0.015f);
        }
        if (puddleMaterial != null)
        {
            Color puddle = puddleMaterial.GetColor("_BaseColor");
            puddle.a = Wetness * 0.42f;
            puddleMaterial.SetColor("_BaseColor", puddle);
        }
        roadSurface?.SetWetness(Wetness);
        terrainSurface?.SetWetness(Wetness * 0.55f);
    }

    public void ApplyWeather(WeatherPreset weather) => ApplyWeather(weather, weather == WeatherPreset.Rain ? 1f : 0f);

    private static void SetRoughnessMapEnabled(Material material, bool available, bool enabled)
    {
        if (!available || material == null) return;
        if (enabled) material.EnableKeyword("_METALLICSPECGLOSSMAP");
        else material.DisableKeyword("_METALLICSPECGLOSSMAP");
    }

    private Color RoadTint => Color.Lerp(profile.roadTint, track.roadColor, profile.snowCoveredGround ? 0.3f : 0.9f);
    private Color GroundTint => Color.Lerp(profile.groundTint, track.groundColor, profile.snowCoveredGround ? 0.25f : 0.82f);
    private Color CurbTint => Color.Lerp(profile.curbPrimary, track.curbColor, 0.88f);

    private void ResolveProfile()
    {
        profile = environmentProfile;
        if (profile == null) profile = Resources.Load<TrackEnvironmentProfile>($"Environment/Profiles/{ProfileId()}");
        bool runtimePreset = profile == null;
        if (runtimePreset) profile = TrackEnvironmentProfile.CreatePreset(ProfileId());
        if (runtimePreset) generatedAssets.Add(profile);
    }

    private string ProfileId()
    {
        return string.IsNullOrEmpty(track.id) ? "winter_departure" : track.id;
    }

    private void CreateMaterials()
    {
        List<Texture2D> generatedTextures = new List<Texture2D>();
        Dictionary<Texture2D, Texture2D> normalCache = new Dictionary<Texture2D, Texture2D>();
        Dictionary<Texture2D, Texture2D> roughnessCache = new Dictionary<Texture2D, Texture2D>();
        roadMaterial = RaceMaterialFactory.CreateSurface("RoadMaterial", profile.roadTexturePrefix, RoadTint, profile.roadTileMeters, 0.16f, generatedTextures, normalCache, roughnessCache);
        groundMaterial = RaceMaterialFactory.CreateSurface("GroundMaterial", profile.groundTexturePrefix, GroundTint, profile.groundTileMeters, 0.06f, generatedTextures, normalCache, roughnessCache);
        snowMaterial = RaceMaterialFactory.CreateSurface("SnowMaterial", profile.snowTexturePrefix, new Color(0.93f, 0.96f, 1f), profile.groundTileMeters, 0.12f, generatedTextures, normalCache, roughnessCache);
        shoulderMaterial = RaceMaterialFactory.CreateSurface("ShoulderMaterial", profile.snowCoveredGround ? profile.snowTexturePrefix : profile.groundTexturePrefix, profile.shoulderTint, profile.groundTileMeters, 0.08f, generatedTextures, normalCache, roughnessCache);
        curbMaterial = RaceMaterialFactory.CreateLit("CurbMaterial", CurbTint, 0.18f);
        curbAlternateMaterial = RaceMaterialFactory.CreateLit("CurbAlternateMaterial", profile.curbSecondary, 0.18f);
        dryRoadTint = roadMaterial.GetColor("_BaseColor");
        dryShoulderTint = shoulderMaterial.GetColor("_BaseColor");
        dryRoadSmoothness = roadMaterial.GetFloat("_Smoothness");
        dryShoulderSmoothness = shoulderMaterial.GetFloat("_Smoothness");
        roadHasRoughnessMap = roadMaterial.GetTexture("_MetallicGlossMap") != null;
        shoulderHasRoughnessMap = shoulderMaterial.GetTexture("_MetallicGlossMap") != null;
        stoneMaterial = RaceMaterialFactory.CreateLit("RetainingStone", new Color(0.42f, 0.43f, 0.39f), 0.08f);
        woodMaterial = RaceMaterialFactory.CreateLit("FenceWood", new Color(0.24f, 0.16f, 0.09f), 0.12f);
        metalMaterial = RaceMaterialFactory.CreateLit("GuardrailSteel", new Color(0.52f, 0.56f, 0.55f), 0.48f, 0.65f);
        bannerMaterial = RaceMaterialFactory.CreateLit("BannerCloth", new Color(0.86f, 0.8f, 0.64f), 0.08f);
        Texture2D checks = CreateCheckerTexture();
        bannerMaterial.SetTexture("_BaseMap", checks);
        RaceMaterialFactory.SetWorldTiling(bannerMaterial, 1f);
        bannerMaterial.EnableKeyword("_BASEMAP");
        darkMaterial = RaceMaterialFactory.CreateLit("StartLineDark", new Color(0.075f, 0.085f, 0.075f), 0.15f);
        glassMaterial = RaceMaterialFactory.CreateLit("WarmWindowGlass", new Color(0.38f, 0.5f, 0.48f, 0.42f), 0.92f);
        RaceMaterialFactory.MakeTransparent(glassMaterial, new Color(0.38f, 0.5f, 0.48f, 0.42f));
        glassMaterial.SetColor("_EmissionColor", new Color(1f, 0.63f, 0.28f) * 0.35f);
        glassMaterial.EnableKeyword("_EMISSION");
        glassMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        foliageMaterial = RaceMaterialFactory.CreateLit("FallbackFoliage", new Color(0.16f, 0.25f, 0.14f), 0.05f);
        rockMaterial = RaceMaterialFactory.CreateLit("AlpineRock", new Color(0.36f, 0.38f, 0.35f), 0.06f);
        warmStoneMaterial = RaceMaterialFactory.CreateLit("WarmStone", new Color(0.66f, 0.54f, 0.39f), 0.1f);
        roofMaterial = RaceMaterialFactory.CreateLit("TerracottaRoof", new Color(0.42f, 0.18f, 0.1f), 0.12f);
        seaMaterial = RaceMaterialFactory.CreateLit("MediterraneanWater", new Color(0.075f, 0.32f, 0.38f, 0.88f), 0.88f, 0.15f);
        RaceMaterialFactory.MakeTransparent(seaMaterial, new Color(0.075f, 0.32f, 0.38f, 0.88f), 2990);
        sailMaterial = RaceMaterialFactory.CreateLit("SailCloth", new Color(0.87f, 0.82f, 0.68f), 0.06f);
        sailMaterial.SetFloat("_Cull", 0f);
        lampMaterial = RaceMaterialFactory.CreateLit("HarborLamp", new Color(1f, 0.66f, 0.25f), 0.15f);
        lampMaterial.SetColor("_EmissionColor", new Color(1f, 0.48f, 0.12f) * 2.5f);
        lampMaterial.EnableKeyword("_EMISSION");
        lampMaterial.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        puddleMaterial = RaceMaterialFactory.CreateLit("RacePuddles", new Color(0.055f, 0.075f, 0.085f, 0f), 0.94f);
        RaceMaterialFactory.MakeTransparent(puddleMaterial, new Color(0.055f, 0.075f, 0.085f, 0f));

        generatedAssets.AddRange(new Object[]
        {
            roadMaterial, groundMaterial, snowMaterial, shoulderMaterial, curbMaterial, curbAlternateMaterial,
            stoneMaterial, woodMaterial, metalMaterial, bannerMaterial, darkMaterial, glassMaterial,
            foliageMaterial, rockMaterial, warmStoneMaterial, roofMaterial, seaMaterial, sailMaterial, lampMaterial, puddleMaterial, checks
        });
        generatedAssets.AddRange(generatedTextures);
    }

    private Texture2D CreateCheckerTexture()
    {
        const int width = 128;
        const int height = 32;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, true, false)
        {
            name = "BannerChecker",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Bilinear
        };
        Color cream = new Color(0.86f, 0.8f, 0.64f);
        Color dark = new Color(0.08f, 0.1f, 0.085f);
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bool border = y < 3 || y >= height - 3 || x < 3 || x >= width - 3;
                pixels[y * width + x] = border || ((x / 8 + y / 8) % 2 == 0) ? dark : cream;
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(true, true);
        return texture;
    }

    private void CreateAtmosphere()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogDensity = profile.fogDensity;
        RenderSettings.fogColor = profile.fogColor;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientSkyColor = Color.Lerp(profile.skyTint, Color.white, 0.15f);
        RenderSettings.ambientEquatorColor = profile.fogColor * 0.72f;
        RenderSettings.ambientGroundColor = profile.skyGroundColor * 0.5f;
        Material skyboxTemplate = Resources.Load<Material>("Effects/ProceduralSkybox");
        Shader skyShader = skyboxTemplate != null ? skyboxTemplate.shader : Shader.Find("Skybox/Procedural");
        if (skyShader != null)
        {
            Material skybox = new Material(skyShader) { name = "RaceProceduralSky" };
            skybox.SetColor("_SkyTint", profile.skyTint);
            skybox.SetColor("_GroundColor", profile.skyGroundColor);
            skybox.SetFloat("_AtmosphereThickness", profile.atmosphereThickness);
            skybox.SetFloat("_Exposure", profile.skyExposure);
            skybox.SetVector("_SunDirection", Quaternion.Euler(48f, -32f, 0f) * Vector3.forward);
            RenderSettings.skybox = skybox;
            generatedAssets.Add(skybox);
        }

        Light[] lights = FindObjectsByType<Light>(FindObjectsInactive.Include);
        Light sun = null;
        foreach (Light light in lights)
        {
            if (light.type != LightType.Directional) continue;
            light.color = profile.sunColor;
            light.intensity = profile.sunIntensity;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.82f;
            light.shadowBias = 0.035f;
            light.shadowNormalBias = 0.35f;
            light.shadowNearPlane = 0.1f;
            UniversalAdditionalLightData lightData = light.GetComponent<UniversalAdditionalLightData>();
            if (lightData != null) lightData.softShadowQuality = SoftShadowQuality.High;
            if (!light.gameObject.activeInHierarchy) continue;
            if (sun == null || light.name == "Directional Light") sun = light;
        }
        if (sun == null)
        {
            GameObject sunObject = new GameObject("Directional Light", typeof(Light));
            sunObject.transform.SetParent(generatedRoot.transform, false);
            sunObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            sun = sunObject.GetComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = profile.sunColor;
            sun.intensity = profile.sunIntensity;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.82f;
        }
        RenderSettings.sun = sun;

        foreach (Camera camera in FindObjectsByType<Camera>(FindObjectsInactive.Include)) ConfigureCamera(camera);
        Camera main = Camera.main;
        if (main == null)
        {
            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(generatedRoot.transform, false);
            cameraObject.transform.position = StartPoint - StartTangent * 8f + Vector3.up * 3.5f;
            cameraObject.transform.LookAt(StartPoint + StartTangent * 5f);
            main = cameraObject.GetComponent<Camera>();
            ConfigureCamera(main);
        }
        main.backgroundColor = profile.fogColor;

        volumeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        volumeProfile.name = "RaceAtmosphere";
        Bloom bloom = volumeProfile.Add<Bloom>(true);
        bloom.intensity.overrideState = true;
        bloom.intensity.value = profile.bloomIntensity;
        bloom.threshold.overrideState = true;
        bloom.threshold.value = 1.05f;
        bloom.scatter.overrideState = true;
        bloom.scatter.value = 0.55f;
        ColorAdjustments color = volumeProfile.Add<ColorAdjustments>(true);
        color.postExposure.overrideState = true;
        color.postExposure.value = profile.exposure;
        color.contrast.overrideState = true;
        color.contrast.value = 12f;
        color.saturation.overrideState = true;
        color.saturation.value = profile.seaEnabled ? 10f : 4f;
        Vignette vignette = volumeProfile.Add<Vignette>(true);
        vignette.intensity.overrideState = true;
        vignette.intensity.value = profile.vignetteIntensity;
        vignette.smoothness.overrideState = true;
        vignette.smoothness.value = 0.35f;
        GameObject volumeObject = new GameObject("AtmosphereVolume", typeof(Volume));
        volumeObject.transform.SetParent(generatedRoot.transform, false);
        Volume volume = volumeObject.GetComponent<Volume>();
        volume.sharedProfile = volumeProfile;
        volume.isGlobal = true;
        volume.priority = 20f;
        generatedAssets.Add(volumeProfile);
    }

    private static void ConfigureCamera(Camera camera)
    {
        UniversalAdditionalCameraData cameraData = camera.GetComponent<UniversalAdditionalCameraData>();
        if (cameraData == null) cameraData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        cameraData.renderPostProcessing = true;
        cameraData.renderShadows = true;
        cameraData.volumeLayerMask = ~0;
        cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        cameraData.antialiasingQuality = AntialiasingQuality.Medium;
        camera.allowHDR = true;
        camera.allowMSAA = false;
        camera.farClipPlane = Mathf.Max(camera.farClipPlane, 1800f);
        camera.clearFlags = CameraClearFlags.Skybox;
    }

    private void CreateGround()
    {
        terrain = new TrackTerrainBuilder();
        terrain.Build(generatedRoot.transform, profile, this, groundMaterial, snowMaterial, track.roadWidth * 0.5f, profile.seed + StableId(track.id ?? string.Empty));
        generatedAssets.Add(terrain.TerrainObject.GetComponent<MeshFilter>().sharedMesh);
        terrainSurface = terrain.TerrainObject.GetComponent<TrackSurface1912>();
    }

    private void CreateSampledPath()
    {
        for (int i = 0; i < sampleCount; i++)
        {
            float progress = i / (float)sampleCount;
            Samples.Add(route.PointAt(progress));
        }
        for (int i = 0; i < sampleCount; i++)
        {
            Vector3 tangent = (Samples[(i + 1) % sampleCount] - Samples[(i - 1 + sampleCount) % sampleCount]).normalized;
            if (tangent.sqrMagnitude < 0.001f) tangent = (Samples[(i + 1) % sampleCount] - Samples[i]).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
            if (right.sqrMagnitude < 0.001f) right = Vector3.right;
            Tangents.Add(tangent);
            rights.Add(right);
            normals.Add(Vector3.Cross(tangent, right).normalized);
        }
        distances.Add(0f);
        for (int i = 0; i < sampleCount; i++)
        {
            TrackLength += Vector3.Distance(Samples[i], Samples[(i + 1) % sampleCount]);
            distances.Add(TrackLength);
        }
    }

    public float RoadHeightOffsetAtDistance(float distance)
    {
        if (profile == null || TrackLength < 0.001f) return RoadSurfaceOffset;
        float wrapped = Mathf.Repeat(distance, TrackLength);
        float startDistance = Mathf.Min(wrapped, TrackLength - wrapped);
        if (startDistance <= StartSafeZoneMeters) return RoadSurfaceOffset;
        float amplitude = Mathf.Min(0.025f, Mathf.Max(0f, profile.roadRoughness) * (profile.roadTexturePrefix.Contains("cobble") ? 1.6f : 1f));
        float envelope = SmoothStep(StartSafeZoneMeters, StartSafeZoneMeters + 10f, wrapped)
            * SmoothStep(StartSafeZoneMeters, StartSafeZoneMeters + 10f, TrackLength - wrapped);
        float relief = Mathf.Sin(wrapped * Mathf.PI * 2f / 28f) * 0.65f
            + Mathf.Sin(wrapped * Mathf.PI * 2f / 37f + 1.1f) * 0.35f;
        return RoadSurfaceOffset + envelope * amplitude * relief;
    }

    private static float SmoothStep(float edge0, float edge1, float value)
    {
        float t = Mathf.Clamp01((value - edge0) / Mathf.Max(0.0001f, edge1 - edge0));
        return t * t * (3f - 2f * t);
    }

    private Vector3[] DefaultLoop()
    {
        return new[]
        {
            new Vector3(-55f, 0f, 35f), new Vector3(-20f, 0f, 5f), new Vector3(15f, 0f, -30f),
            new Vector3(55f, -15f, 0f), new Vector3(50f, 35f, 35f), new Vector3(10f, 60f, 60f), new Vector3(-40f, 55f, 55f)
        };
    }

    private void CreateRoadMesh()
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> road = new List<int>();
        List<int> shoulders = new List<int>();
        List<int> support = new List<int>();
        float half = track.roadWidth * 0.5f;
        float shoulder = profile.shoulderWidth;
        for (int i = 0; i < sampleCount; i++)
        {
            int nextIndex = (i + 1) % sampleCount;
            Vector3 point = Samples[i];
            Vector3 next = Samples[nextIndex];
            Vector3 right = rights[i];
            Vector3 nextRight = rights[nextIndex];
            Vector3 normal = normals[i];
            Vector3 nextNormal = normals[nextIndex];
            float segmentLength = Vector3.Distance(point, next);
            float distance = distances[i];
            float nextDistance = i + 1 < sampleCount ? distances[i + 1] : TrackLength;
            float roadOffset = RoadHeightOffsetAtDistance(distance);
            float nextRoadOffset = RoadHeightOffsetAtDistance(nextDistance);
            Vector3 leftRoad = point - right * half + normal * roadOffset;
            Vector3 rightRoad = point + right * half + normal * roadOffset;
            Vector3 leftShoulder = point - right * (half + shoulder) + normal * (roadOffset - 0.06f);
            Vector3 rightShoulder = point + right * (half + shoulder) + normal * (roadOffset - 0.06f);
            Vector3 leftGround = new Vector3(leftShoulder.x, terrain.SampleHeight(leftShoulder) - 0.08f, leftShoulder.z);
            Vector3 rightGround = new Vector3(rightShoulder.x, terrain.SampleHeight(rightShoulder) - 0.08f, rightShoulder.z);
            Vector3 nl = next - nextRight * half + nextNormal * nextRoadOffset;
            Vector3 nr = next + nextRight * half + nextNormal * nextRoadOffset;
            Vector3 nls = next - nextRight * (half + shoulder) + nextNormal * (nextRoadOffset - 0.06f);
            Vector3 nrs = next + nextRight * (half + shoulder) + nextNormal * (nextRoadOffset - 0.06f);
            Vector3 nlg = new Vector3(nls.x, terrain.SampleHeight(nls) - 0.08f, nls.z);
            Vector3 nrg = new Vector3(nrs.x, terrain.SampleHeight(nrs) - 0.08f, nrs.z);
            TrackMeshBuilder.AddQuad(vertices, uvs, road, leftRoad, nl, nr, rightRoad, 0f, distance, track.roadWidth, segmentLength);
            TrackMeshBuilder.AddQuad(vertices, uvs, shoulders, leftRoad, leftShoulder, nls, nl, 0f, distance, shoulder, segmentLength);
            TrackMeshBuilder.AddQuad(vertices, uvs, shoulders, rightRoad, nr, nrs, rightShoulder, half, distance, shoulder, segmentLength);
            TrackMeshBuilder.AddQuad(vertices, uvs, support, leftShoulder, leftGround, nlg, nls, 0f, distance, shoulder, segmentLength);
            TrackMeshBuilder.AddQuad(vertices, uvs, support, rightShoulder, nrs, nrg, rightGround, half, distance, shoulder, segmentLength);
            TrackMeshBuilder.AddQuad(vertices, uvs, support, leftGround, rightGround, nrg, nlg, 0f, distance, track.roadWidth + shoulder * 2f, segmentLength);

            if (profile.retainingWalls)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 top = point + right * side * (half + shoulder + profile.barrierOffset) + normal * (roadOffset - 0.025f);
                    Vector3 nextTop = next + nextRight * side * (half + shoulder + profile.barrierOffset) + nextNormal * (nextRoadOffset - 0.025f);
                    Vector3 shoulderTop = point + right * side * (half + shoulder) + normal * (roadOffset - 0.06f);
                    Vector3 nextShoulderTop = next + nextRight * side * (half + shoulder) + nextNormal * (nextRoadOffset - 0.06f);
                    Vector3 bottom = new Vector3(top.x, terrain.SampleHeight(top) - 0.12f, top.z);
                    Vector3 nextBottom = new Vector3(nextTop.x, terrain.SampleHeight(nextTop) - 0.12f, nextTop.z);
                    if (side < 0) TrackMeshBuilder.AddQuad(vertices, uvs, support, shoulderTop, top, nextTop, nextShoulderTop, 0f, distance, profile.barrierOffset, segmentLength);
                    else TrackMeshBuilder.AddQuad(vertices, uvs, support, shoulderTop, nextShoulderTop, nextTop, top, 0f, distance, profile.barrierOffset, segmentLength);
                    if (top.y - bottom.y > 0.3f)
                    {
                        if (side < 0) TrackMeshBuilder.AddQuad(vertices, uvs, support, top, bottom, nextBottom, nextTop, 0f, distance, profile.barrierOffset, segmentLength);
                        else TrackMeshBuilder.AddQuad(vertices, uvs, support, top, nextTop, nextBottom, bottom, 0f, distance, profile.barrierOffset, segmentLength);
                    }
                    if (i % 8 == 0 && top.y - bottom.y > 0.55f)
                    {
                        Quaternion rotation = Quaternion.LookRotation(Tangents[i], normal);
                        TrackMeshBuilder.AddBox(vertices, uvs, support, (top + bottom) * 0.5f + right * side * 0.24f, rotation, new Vector3(0.55f, top.y - bottom.y, 0.9f));
                    }
                }
            }
        }
        Material[] materials = { roadMaterial, shoulderMaterial, profile.retainingWalls ? stoneMaterial : groundMaterial };
        Mesh mesh = TrackMeshBuilder.Create("TrackRoad", vertices, uvs, new[] { road, shoulders, support }, materials);
        generatedAssets.Add(mesh);
        GameObject roadObject = TrackMeshBuilder.CreateObject("Road", mesh, materials, generatedRoot.transform, true, true);
        roadSurface = roadObject.AddComponent<TrackSurface1912>();
        if (profile.roadTexturePrefix.Contains("snow"))
            roadSurface.Configure(TrackSurface1912.SurfaceKind.Snow, track.surfaceGrip, 1.35f);
        else if (profile.roadTexturePrefix.Contains("cobble"))
            roadSurface.Configure(TrackSurface1912.SurfaceKind.Cobblestone, track.surfaceGrip, 1.18f);
        else
            roadSurface.Configure(TrackSurface1912.SurfaceKind.Dirt, track.surfaceGrip, 1f);
    }

    private void CreateCurbs()
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> primary = new List<int>();
        List<int> alternate = new List<int>();
        float half = track.roadWidth * 0.5f;
        for (int i = 0; i < sampleCount; i++)
        {
            int next = (i + 1) % sampleCount;
            Vector3 point = Samples[i];
            Vector3 nextPoint = Samples[next];
            Vector3 direction = nextPoint - point;
            float length = direction.magnitude;
            if (length < 0.001f) continue;
            Quaternion rotation = Quaternion.LookRotation(direction / length, normals[i]);
            Vector3 center = (point + nextPoint) * 0.5f;
            List<int> target = profile.alternatingCurbs && (i / 2) % 2 == 1 ? alternate : primary;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 right = rights[i];
                Vector3 normal = normals[i];
                float roadOffset = RoadHeightOffsetAtDistance(distances[i] + length * 0.5f);
                Vector3 position = center + right * side * (half + profile.curbWidth * 0.5f) + normal * (roadOffset - 0.045f + profile.curbHeight * 0.5f);
                TrackMeshBuilder.AddBox(vertices, uvs, target, position, rotation, new Vector3(profile.curbWidth, profile.curbHeight, length + 0.035f));
            }
        }
        Material[] materials = { curbMaterial, curbAlternateMaterial };
        Mesh mesh = TrackMeshBuilder.Create("ContinuousCurbs", vertices, uvs, new[] { primary, alternate }, materials);
        generatedAssets.Add(mesh);
        TrackMeshBuilder.CreateObject("Curbs", mesh, materials, generatedRoot.transform, true, true);
    }

    private void CreateBarriers()
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> timber = new List<int>();
        List<int> steel = new List<int>();
        List<int> masonry = new List<int>();
        float offset = track.roadWidth * 0.5f + profile.shoulderWidth + profile.barrierOffset;
        float nextPostDistance = 0f;
        for (int i = 0; i < sampleCount; i++)
        {
            int next = (i + 1) % sampleCount;
            Vector3 point = Samples[i];
            Vector3 nextPoint = Samples[next];
            Vector3 right = rights[i];
            Vector3 nextRight = rights[next];
            Vector3 normal = normals[i];
            Vector3 nextNormal = normals[next];
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 a = point + right * side * offset + normal * (RoadHeightOffsetAtDistance(distances[i]) - 0.025f);
                Vector3 b = nextPoint + nextRight * side * offset + nextNormal * (RoadHeightOffsetAtDistance(i + 1 < sampleCount ? distances[i + 1] : TrackLength) - 0.025f);
                if (profile.barrierStyle == TrackEnvironmentProfile.BarrierStyle.StonePromenade)
                {
                    Vector3 groundA = new Vector3(a.x, Mathf.Max(terrain.SampleHeight(a), point.y - 0.08f), a.z);
                    Vector3 groundB = new Vector3(b.x, Mathf.Max(terrain.SampleHeight(b), nextPoint.y - 0.08f), b.z);
                    TrackMeshBuilder.AddBeam(vertices, uvs, masonry, groundA + Vector3.up * 0.5f, groundB + Vector3.up * 0.5f, 0.42f, 1f, normal);
                    TrackMeshBuilder.AddBeam(vertices, uvs, masonry, groundA + Vector3.up * 1.03f, groundB + Vector3.up * 1.03f, 0.5f, 0.12f, normal);
                }
                else
                {
                    List<int> rails = profile.barrierStyle == TrackEnvironmentProfile.BarrierStyle.Guardrail ? steel : timber;
                    TrackMeshBuilder.AddBeam(vertices, uvs, rails, a + Vector3.up * 0.58f, b + Vector3.up * 0.58f, profile.barrierStyle == TrackEnvironmentProfile.BarrierStyle.Guardrail ? 0.16f : 0.12f, 0.12f, normal);
                    TrackMeshBuilder.AddBeam(vertices, uvs, rails, a + Vector3.up * 0.98f, b + Vector3.up * 0.98f, profile.barrierStyle == TrackEnvironmentProfile.BarrierStyle.Guardrail ? 0.18f : 0.12f, 0.14f, normal);
                    if (profile.barrierStyle == TrackEnvironmentProfile.BarrierStyle.WoodFence)
                    {
                        Quaternion rotation = Quaternion.LookRotation(Tangents[i], normal);
                        float picketGround = Mathf.Max(terrain.SampleHeight(a), point.y - 0.08f);
                        TrackMeshBuilder.AddBox(vertices, uvs, timber, new Vector3(a.x, picketGround + 0.52f, a.z), rotation, new Vector3(0.075f, 1.04f, 0.11f));
                    }
                }
            }
            if (distances[i] >= nextPostDistance)
            {
                nextPostDistance = distances[i] + profile.barrierPostSpacing;
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 position = point + right * side * offset;
                    float ground = Mathf.Max(terrain.SampleHeight(position), point.y - 0.08f);
                    float height = 1.18f;
                    List<int> target = profile.barrierStyle == TrackEnvironmentProfile.BarrierStyle.StonePromenade ? masonry : profile.barrierStyle == TrackEnvironmentProfile.BarrierStyle.Guardrail ? steel : timber;
                    TrackMeshBuilder.AddBox(vertices, uvs, target, new Vector3(position.x, ground + height * 0.5f, position.z), Quaternion.LookRotation(Tangents[i], Vector3.up), new Vector3(0.16f, height, 0.16f));
                }
            }
        }
        Material[] materials = { woodMaterial, metalMaterial, stoneMaterial };
        Mesh mesh = TrackMeshBuilder.Create("ContinuousRoadsideBarriers", vertices, uvs, new[] { timber, steel, masonry }, materials);
        generatedAssets.Add(mesh);
        TrackMeshBuilder.CreateObject("RoadsideBarriers", mesh, materials, generatedRoot.transform, true, true);
    }

    private void CreateStartGantry()
    {
        Vector3 point = StartPoint;
        Vector3 tangent = StartTangent;
        Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
        Vector3 normal = Vector3.Cross(tangent, right).normalized;
        Quaternion rotation = Quaternion.LookRotation(tangent, normal);
        GameObject root = new GameObject("StartGantry");
        root.transform.SetParent(generatedRoot.transform, false);
        root.transform.SetPositionAndRotation(point + normal * RoadHeightOffsetAtDistance(0f), rotation);
        GameObject detailedArch = Resources.Load<GameObject>("Environment/ReleaseKit/Prop_Start_Arch_1912");
        if (detailedArch != null)
        {
            GameObject arch = TrackSceneryBuilder.InstantiateAsset(detailedArch, Vector3.zero, Quaternion.identity, root.transform);
            arch.name = "StartArch_Detailed";
            ConfigureArchWidth(arch, track.roadWidth);
            TrackSceneryBuilder.SetStatic(arch, false);
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject support = new GameObject("StartSupportCollider");
                support.transform.SetParent(root.transform, false);
                support.transform.localPosition = new Vector3(side * (track.roadWidth * 0.5f + 0.85f), 2.5f, 0f);
                BoxCollider collider = support.AddComponent<BoxCollider>();
                collider.size = new Vector3(0.55f, 5f, 0.75f);
                support.isStatic = true;
            }
            CreateStartTiles(root.transform);
            return;
        }
        for (int side = -1; side <= 1; side += 2)
        {
            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillar.name = "StartPillar";
            pillar.transform.SetParent(root.transform, false);
            pillar.transform.localPosition = new Vector3(side * (track.roadWidth * 0.5f + 0.85f), 2.435f, 0f);
            pillar.transform.localScale = new Vector3(0.34f, 5f, 0.34f);
            pillar.GetComponent<MeshRenderer>().sharedMaterial = woodMaterial;
            TrackSceneryBuilder.SetStatic(pillar);
        }
        GameObject banner = GameObject.CreatePrimitive(PrimitiveType.Cube);
        banner.name = "StartBanner";
        banner.transform.SetParent(root.transform, false);
        banner.transform.localPosition = new Vector3(0f, 4.82f, 0f);
        banner.transform.localScale = new Vector3(track.roadWidth + 1.7f, 0.82f, 0.22f);
        banner.GetComponent<MeshRenderer>().sharedMaterial = bannerMaterial;
        banner.GetComponent<Collider>().enabled = false;
        banner.isStatic = true;

        GameObject labelObject = new GameObject("StartBannerLabel", typeof(RectTransform), typeof(Canvas));
        labelObject.transform.SetParent(root.transform, false);
        labelObject.transform.localPosition = new Vector3(0f, 4.82f, -0.13f);
        labelObject.transform.localScale = Vector3.one * 0.01f;
        labelObject.GetComponent<RectTransform>().sizeDelta = new Vector2(1024f, 80f);
        Canvas labelCanvas = labelObject.GetComponent<Canvas>();
        labelCanvas.renderMode = RenderMode.WorldSpace;
        labelCanvas.sortingOrder = 1;
        UIUtils.Text(labelCanvas.transform, "Label", string.IsNullOrEmpty(track.displayName) ? "RUSSO-BALT" : track.displayName.ToUpperInvariant(), 64, UITheme.Cream, TextAnchor.MiddleCenter, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, UITheme.TitleFont);
        CreateStartTiles(root.transform);
    }

    private static void ConfigureArchWidth(GameObject arch, float roadWidth)
    {
        Vector3 sourceScale = arch.transform.localScale;
        float nativeWidth = 9.4f * Mathf.Max(0.0001f, Mathf.Abs(sourceScale.x));
        float beamScale = (roadWidth + 1.7f) / nativeWidth;
        arch.transform.localScale = new Vector3(sourceScale.x * beamScale, sourceScale.y, sourceScale.z);
    }

    private void CreateStartTiles(Transform root)
    {
        for (int i = 0; i < 16; i++)
        {
            GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.name = "StartLineTile";
            tile.transform.SetParent(root, false);
            float width = track.roadWidth * 0.96f / 16f;
            tile.transform.localPosition = new Vector3(-track.roadWidth * 0.48f + (i + 0.5f) * width, 0.0145f, 0f);
            tile.transform.localScale = new Vector3(width, 0.025f, 0.7f);
            tile.GetComponent<MeshRenderer>().sharedMaterial = i % 2 == 0 ? bannerMaterial : darkMaterial;
            tile.GetComponent<Collider>().enabled = false;
            tile.isStatic = true;
        }
    }

    private void CreateCheckpoints()
    {
        float half = track.roadWidth * 0.5f;
        for (int i = 0; i < checkpointCount; i++)
        {
            int sample = Mathf.RoundToInt(i / (float)checkpointCount * sampleCount) % sampleCount;
            Vector3 normal = normals[sample];
            Quaternion rotation = Quaternion.LookRotation(Tangents[sample], normal);
            GameObject checkpoint = new GameObject($"Checkpoint_{i:00}", typeof(Checkpoint));
            checkpoint.transform.SetParent(generatedRoot.transform, false);
            checkpoint.transform.SetPositionAndRotation(Samples[sample] + normal * RoadHeightOffsetAtDistance(distances[sample]), rotation);
            Checkpoint component = checkpoint.GetComponent<Checkpoint>();
            component.index = i;
            SphereCollider legacyTrigger = checkpoint.GetComponent<SphereCollider>();
            if (legacyTrigger != null) legacyTrigger.enabled = false;
            BoxCollider trigger = checkpoint.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 3f, 0f);
            trigger.size = new Vector3(track.roadWidth + profile.shoulderWidth * 2f + 1.5f, 6f, 3.5f);

            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            marker.name = "Marker";
            marker.transform.SetParent(checkpoint.transform, false);
            marker.transform.localPosition = new Vector3(0f, 4.65f, 0f);
            marker.transform.localScale = new Vector3(track.roadWidth + 0.9f, 0.2f, 0.2f);
            marker.GetComponent<MeshRenderer>().sharedMaterial = bannerMaterial;
            marker.GetComponent<Collider>().enabled = false;
            marker.isStatic = true;
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                post.name = "CheckpointPost";
                post.transform.SetParent(checkpoint.transform, false);
                post.transform.localPosition = new Vector3(side * (half + 0.38f), 2.25f, 0f);
                post.transform.localScale = new Vector3(0.16f, 4.65f, 0.16f);
                post.GetComponent<MeshRenderer>().sharedMaterial = metalMaterial;
                post.GetComponent<Collider>().enabled = false;
                post.isStatic = true;
            }
        }
    }

    private void CreateScenery()
    {
        TrackSceneryBuilder.Build(generatedRoot.transform, profile, this, terrain, random, snowMaterial, rockMaterial, stoneMaterial, warmStoneMaterial, roofMaterial, glassMaterial, woodMaterial, foliageMaterial, sailMaterial, metalMaterial, lampMaterial, seaMaterial, groundMaterial);
        foreach (Transform child in generatedRoot.transform)
        {
            if (child.name.StartsWith("Horizon_") || child.name.StartsWith("TrackSection_") || child.name == "Rocks" || child.name == "Sea" || child.name == "Harbor" || child.name == "DistantGround" || child.name == "Building_Fallback")
                RegisterRuntimeAssets(child.gameObject);
        }
        foreach (MeshFilter filter in generatedRoot.GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh != null && (filter.sharedMesh.name == "TreeTrunk" || filter.sharedMesh.name == "TreeCrown" || filter.sharedMesh.name == "SnowCap" || filter.sharedMesh.name == "StoneBuilding") && !generatedAssets.Contains(filter.sharedMesh))
                generatedAssets.Add(filter.sharedMesh);
        }
    }

    private void CreateTrackSections()
    {
        TrackSceneryBuilder.BuildSections(generatedRoot.transform, profile, this, terrain, random,
            bannerMaterial, metalMaterial, woodMaterial, stoneMaterial, warmStoneMaterial, roofMaterial, glassMaterial);
    }

    private void RegisterRuntimeAssets(GameObject root)
    {
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null || generatedAssets.Contains(filter.sharedMesh)) continue;
            bool generatedSectionMesh = root.name.StartsWith("TrackSection_") && filter.sharedMesh.name.StartsWith("TrackSection_");
            bool generatedHorizonMesh = filter.sharedMesh.name.StartsWith("Horizon") || filter.sharedMesh.name == "Sea" || filter.sharedMesh.name == "Harbor" || filter.sharedMesh.name == "DistantGround" || filter.sharedMesh.name == "StoneBuilding" || filter.sharedMesh.name == "AlpineRocks";
            if ((generatedSectionMesh || generatedHorizonMesh) && filter.sharedMesh.name != "Cube") generatedAssets.Add(filter.sharedMesh);
        }
        foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>())
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material != null && material.name.StartsWith("Horizon") && !generatedAssets.Contains(material)) generatedAssets.Add(material);
            }
        }
    }

    private void CreateRoadsideDetails()
    {
        GameObject sign = Resources.Load<GameObject>("Environment/Sign");
        GameObject cart = Resources.Load<GameObject>("Environment/Cart");
        float nextSign = 24f;
        float nextCart = 85f;
        float nextPole = 18f;
        float nextMilestone = 55f;
        float nextHaystack = 95f;
        for (int i = 0; i < sampleCount; i++)
        {
            if (distances[i] >= nextSign)
            {
                nextSign = distances[i] + TrackMeshBuilder.RandomRange(random, 32f, 48f);
                if (sign != null)
                {
                    int side = random.Next(2) == 0 ? -1 : 1;
                    Vector3 position = Samples[i] + rights[i] * side * (track.roadWidth * 0.5f + profile.shoulderWidth + 2f);
                    position.y = terrain.SampleHeight(position);
                    Quaternion rotation = Quaternion.LookRotation(-Tangents[i], Vector3.up);
                    GameObject instance = TrackSceneryBuilder.InstantiateAsset(sign, position, rotation, generatedRoot.transform);
                    TrackSceneryBuilder.GroundInstance(instance, position.y);
                    instance.name = "RoadSign";
                    TrackSceneryBuilder.SetStatic(instance);
                    DestroyIfUnsafe(instance, false);
                }
            }
            if (distances[i] >= nextCart)
            {
                nextCart = distances[i] + TrackMeshBuilder.RandomRange(random, 95f, 140f);
                if (cart != null)
                {
                    int side = random.Next(2) == 0 ? -1 : 1;
                    Vector3 position = Samples[i] + rights[i] * side * (track.roadWidth * 0.5f + 4.5f);
                    position.y = terrain.SampleHeight(position);
                    GameObject instance = TrackSceneryBuilder.InstantiateAsset(cart, position, Quaternion.LookRotation(Tangents[i], Vector3.up), generatedRoot.transform);
                    TrackSceneryBuilder.GroundInstance(instance, position.y);
                    instance.name = "HorseCart";
                    TrackSceneryBuilder.SetStatic(instance);
                    DestroyIfUnsafe(instance, false);
                }
            }
            if (distances[i] >= nextPole)
            {
                nextPole = distances[i] + 46f;
                int side = random.Next(2) == 0 ? -1 : 1;
                Vector3 position = Samples[i] + rights[i] * side * (track.roadWidth * 0.5f + profile.shoulderWidth + 5.5f);
                PlaceRoadsideProp("Environment/ReleaseKit/Prop_Telegraph_Pole_01", position, Quaternion.LookRotation(Tangents[i], Vector3.up), "TelegraphPole");
            }
            if (distances[i] >= nextMilestone)
            {
                nextMilestone = distances[i] + 72f;
                int side = random.Next(2) == 0 ? -1 : 1;
                Vector3 position = Samples[i] + rights[i] * side * (track.roadWidth * 0.5f + profile.shoulderWidth + 2.4f);
                PlaceRoadsideProp("Environment/ReleaseKit/Prop_Milestone_01", position, Quaternion.LookRotation(-Tangents[i], Vector3.up), "Milestone");
            }
            if (!profile.seaEnabled && distances[i] >= nextHaystack)
            {
                nextHaystack = distances[i] + TrackMeshBuilder.RandomRange(random, 95f, 135f);
                int side = random.Next(2) == 0 ? -1 : 1;
                Vector3 position = Samples[i] + rights[i] * side * (track.roadWidth * 0.5f + 9f + random.Next(0, 4));
                PlaceRoadsideProp("Environment/ReleaseKit/Prop_Haystack_01", position, Quaternion.Euler(0f, random.Next(0, 360), 0f), "Haystack");
            }
        }
    }

    private void CreatePuddles()
    {
        List<Vector3> vertices = new List<Vector3>();
        List<Vector2> uvs = new List<Vector2>();
        List<int> triangles = new List<int>();
        System.Random puddleRandom = new System.Random(unchecked(profile.seed + 903));
        const int segments = 14;
        float distance = 28f;
        while (distance < TrackLength - 22f)
        {
            distance += TrackMeshBuilder.RandomRange(puddleRandom, 22f, 38f);
            if (distance >= TrackLength - 22f) break;
            FrameAtDistance(distance, out Vector3 point, out Vector3 tangent, out Vector3 right, out Vector3 normal);
            float lateral = TrackMeshBuilder.RandomRange(puddleRandom, -track.roadWidth * 0.32f, track.roadWidth * 0.32f);
            float radiusX = TrackMeshBuilder.RandomRange(puddleRandom, 1.1f, 2.4f);
            float radiusZ = TrackMeshBuilder.RandomRange(puddleRandom, 2.8f, 5.8f);
            Quaternion rotation = Quaternion.LookRotation(tangent, normal);
            Vector3 center = point + right * lateral + normal * (RoadHeightOffsetAtDistance(distance) + 0.006f);
            int centerIndex = vertices.Count;
            vertices.Add(center);
            uvs.Add(new Vector2(0.5f, 0.5f));
            for (int i = 0; i <= segments; i++)
            {
                float angle = i / (float)segments * Mathf.PI * 2f;
                Vector3 offset = rotation * new Vector3(Mathf.Cos(angle) * radiusX, 0f, Mathf.Sin(angle) * radiusZ);
                vertices.Add(center + offset);
                uvs.Add(new Vector2(0.5f + Mathf.Cos(angle) * 0.5f, 0.5f + Mathf.Sin(angle) * 0.5f));
                if (i < segments) triangles.AddRange(new[] { centerIndex, centerIndex + i + 2, centerIndex + i + 1 });
            }
        }
        if (triangles.Count == 0) return;
        Mesh mesh = TrackMeshBuilder.Create("RoadPuddles", vertices, uvs, new[] { triangles }, new[] { puddleMaterial });
        generatedAssets.Add(mesh);
        TrackMeshBuilder.CreateObject("RoadPuddles", mesh, new[] { puddleMaterial }, generatedRoot.transform, false, false);
    }

    private void PlaceRoadsideProp(string resource, Vector3 position, Quaternion rotation, string name)
    {
        GameObject prefab = Resources.Load<GameObject>(resource);
        if (prefab == null || IsStartSafeZone(position)) return;
        position.y = terrain.SampleHeight(position);
        GameObject instance = TrackSceneryBuilder.InstantiateAsset(prefab, position, rotation, generatedRoot.transform);
        instance.name = name;
        TrackSceneryBuilder.GroundInstance(instance, position.y);
        TrackSceneryBuilder.SetStatic(instance);
        DestroyIfUnsafe(instance, false);
    }

    private void DestroyIfUnsafe(GameObject instance, bool includeVisualBounds)
    {
        if (TrackSceneryBuilder.HasRoadClearance(instance, this, includeVisualBounds)) return;
        if (Application.isPlaying) Destroy(instance);
        else DestroyImmediate(instance);
    }

    public Vector3 PointAt(float progress)
    {
        return PointAtDistance(Mathf.Repeat(progress, 1f) * TrackLength);
    }

    public Vector3 PointAtDistance(float distance)
    {
        if (Samples.Count == 0 || TrackLength < 0.001f) return Vector3.zero;
        float wrapped = Mathf.Repeat(distance, TrackLength);
        int index = FindSampleIndex(wrapped);
        int next = (index + 1) % sampleCount;
        float segmentLength = (next == 0 ? TrackLength : distances[next]) - distances[index];
        float t = segmentLength > 0.0001f ? (wrapped - distances[index]) / segmentLength : 0f;
        return Vector3.Lerp(Samples[index], Samples[next], t);
    }

    public Vector3 TangentAt(float progress) => TangentAtDistance(Mathf.Repeat(progress, 1f) * TrackLength);

    public Vector3 TangentAtDistance(float distance)
    {
        if (Samples.Count == 0 || TrackLength < 0.001f) return Vector3.forward;
        int index = FindSampleIndex(Mathf.Repeat(distance, TrackLength));
        return Vector3.Lerp(Tangents[index], Tangents[(index + 1) % sampleCount], 0.5f).normalized;
    }

    public Vector3 RightAt(float progress)
    {
        Vector3 tangent = TangentAt(progress);
        Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
        return right.sqrMagnitude > 0.001f ? right : Vector3.right;
    }

    public Vector3 NormalAt(float progress) => Vector3.Cross(TangentAt(progress), RightAt(progress)).normalized;

    public void FrameAtDistance(float distance, out Vector3 point, out Vector3 tangent, out Vector3 right, out Vector3 normal)
    {
        if (Samples.Count == 0 || TrackLength < 0.001f)
        {
            point = Vector3.zero;
            tangent = Vector3.forward;
            right = Vector3.right;
            normal = Vector3.up;
            return;
        }
        float wrapped = Mathf.Repeat(distance, TrackLength);
        int index = FindSampleIndex(wrapped);
        int next = (index + 1) % sampleCount;
        float segmentLength = (next == 0 ? TrackLength : distances[next]) - distances[index];
        float t = segmentLength > 0.0001f ? Mathf.Clamp01((wrapped - distances[index]) / segmentLength) : 0f;
        point = Vector3.Lerp(Samples[index], Samples[next], t);
        tangent = Vector3.Lerp(Tangents[index], Tangents[next], t).normalized;
        right = Vector3.Cross(Vector3.up, tangent).normalized;
        if (right.sqrMagnitude < 0.001f) right = Vector3.right;
        normal = Vector3.Cross(tangent, right).normalized;
    }

    public float NearestProgress(Vector3 position, out float lateralDistance, out float centerDistance)
    {
        float along;
        Vector3 point;
        Vector3 tangent;
        NearestRoad(position, out along, out lateralDistance, out centerDistance, out point, out tangent);
        return TrackLength > 0.001f ? along / TrackLength : 0f;
    }

    public float DistanceAlongTrack(Vector3 position)
    {
        float lateral;
        float center;
        Vector3 point;
        Vector3 tangent;
        NearestRoad(position, out float along, out lateral, out center, out point, out tangent);
        return along;
    }

    public float RoadDistanceAt(Vector3 position)
    {
        float lateral;
        Vector3 point;
        Vector3 tangent;
        NearestRoad(position, out _, out lateral, out float center, out point, out tangent);
        return center;
    }

    public float RoadHeightAt(Vector3 position)
    {
        NearestRoad(position, out float along, out _, out _, out Vector3 point, out Vector3 tangent);
        Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
        Vector3 normal = Vector3.Cross(tangent, right).normalized;
        return point.y + normal.y * RoadHeightOffsetAtDistance(along);
    }

    public bool HasRoadClearance(Vector3 position, float radius)
    {
        return RoadDistanceAt(position) >= track.roadWidth * 0.5f + profile.shoulderWidth + profile.barrierOffset + Mathf.Max(0f, radius) + 0.65f;
    }

    public bool IsStartSafeZone(Vector3 position)
    {
        if (Samples.Count == 0) return true;
        NearestRoad(position, out float along, out _, out _, out _, out _);
        float circularDistance = Mathf.Min(along, TrackLength - along);
        return circularDistance <= StartSafeZoneMeters;
    }

    public void GridPose(int slot, out Vector3 position, out Quaternion rotation)
    {
        if (Samples.Count == 0 || TrackLength < 0.001f)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            return;
        }
        int row = Mathf.Max(0, slot) / 2;
        int column = Mathf.Max(0, slot) % 2;
        float lateral = column == 0 ? -1f : 1f;
        lateral *= Mathf.Clamp(track.roadWidth * 0.24f, 1.65f, track.roadWidth * 0.5f - 1.25f);
        float distance = Mathf.Repeat(-6.5f - row * 6.8f, TrackLength);
        FrameAtDistance(distance, out Vector3 point, out Vector3 tangent, out Vector3 right, out Vector3 normal);
        position = point + right * lateral + normal * RoadHeightOffsetAtDistance(distance);
        rotation = Quaternion.LookRotation(tangent, normal);
    }

    private int FindSampleIndex(float distance)
    {
        int low = 0;
        int high = sampleCount - 1;
        while (low < high)
        {
            int middle = (low + high) / 2;
            if (distances[middle + 1] < distance) low = middle + 1;
            else high = middle;
        }
        return Mathf.Clamp(low, 0, sampleCount - 1);
    }

    private void NearestRoad(Vector3 position, out float along, out float lateral, out float centerDistance, out Vector3 point, out Vector3 tangent)
    {
        float bestSquared = float.PositiveInfinity;
        along = 0f;
        lateral = 0f;
        point = Samples.Count > 0 ? Samples[0] : Vector3.zero;
        tangent = Tangents.Count > 0 ? Tangents[0] : Vector3.forward;
        for (int i = 0; i < Samples.Count; i++)
        {
            int next = (i + 1) % Samples.Count;
            Vector3 a = Samples[i];
            Vector3 b = Samples[next];
            float abx = b.x - a.x;
            float abz = b.z - a.z;
            float lengthSquared = abx * abx + abz * abz;
            float t = lengthSquared > 0.0001f ? Mathf.Clamp01(((position.x - a.x) * abx + (position.z - a.z) * abz) / lengthSquared) : 0f;
            Vector3 closest = Vector3.Lerp(a, b, t);
            float dx = position.x - closest.x;
            float dz = position.z - closest.z;
            float candidate = dx * dx + dz * dz;
            if (candidate >= bestSquared) continue;
            bestSquared = candidate;
            float segmentLength = (next == 0 ? TrackLength : distances[next]) - distances[i];
            along = distances[i] + segmentLength * t;
            Vector3 segmentTangent = (b - a).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, segmentTangent).normalized;
            lateral = Vector3.Dot(position - closest, right);
            point = closest;
            tangent = segmentTangent;
        }
        centerDistance = Mathf.Sqrt(bestSquared);
    }

    private static int StableId(string value)
    {
        unchecked
        {
            int hash = 17;
            foreach (char c in value) hash = hash * 31 + c;
            return hash;
        }
    }

    private void ClearGenerated()
    {
        TrackSceneryBuilder.ClearCulling();
        if (generatedRoot != null)
        {
            generatedRoot.SetActive(false);
            if (Application.isPlaying) Destroy(generatedRoot);
            else DestroyImmediate(generatedRoot);
            generatedRoot = null;
        }
        foreach (Object asset in generatedAssets)
        {
            if (asset == null) continue;
            if (Application.isPlaying) Destroy(asset);
            else DestroyImmediate(asset);
        }
        generatedAssets.Clear();
        terrain = null;
        volumeProfile = null;
        EffectiveSections = new TrackData.TrackSection[0];
        roadSurface = null;
        terrainSurface = null;
        puddleMaterial = null;
        Wetness = 0f;
    }

    private void OnDestroy()
    {
        TrackSceneryBuilder.ClearCulling();
        foreach (Object asset in generatedAssets)
        {
            if (asset != null) Destroy(asset);
        }
        generatedAssets.Clear();
    }
}
