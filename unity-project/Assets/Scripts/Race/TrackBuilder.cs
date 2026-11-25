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
    public float TrackLength { get; private set; }
    public Vector3 StartPoint => Samples.Count > 0 ? Samples[0] : Vector3.zero;
    public Vector3 StartTangent => Tangents.Count > 0 ? Tangents[0] : Vector3.forward;

    private readonly List<Vector3> rights = new List<Vector3>();
    private readonly List<Vector3> normals = new List<Vector3>();
    private readonly List<float> distances = new List<float>();
    private readonly List<Object> generatedAssets = new List<Object>();
    private GameObject generatedRoot;
    private TrackEnvironmentProfile profile;
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

        ResolveProfile();
        generatedRoot = new GameObject("Track_Generated");
        generatedRoot.transform.SetParent(transform, false);
        random = new System.Random(unchecked(profile.seed + StableId(track.id ?? string.Empty)));
        CreateMaterials();
        CreateSampledPath();
        CreateAtmosphere();
        CreateGround();
        CreateRoadMesh();
        CreateCurbs();
        CreateBarriers();
        CreateStartGantry();
        CreateCheckpoints();
        CreateScenery();
        CreateRoadsideDetails();
    }

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
        roadMaterial = RaceMaterialFactory.CreateSurface("RoadMaterial", profile.roadTexturePrefix, profile.roadTint, profile.roadTileMeters, 0.16f, generatedTextures, normalCache, roughnessCache);
        groundMaterial = RaceMaterialFactory.CreateSurface("GroundMaterial", profile.groundTexturePrefix, profile.groundTint, profile.groundTileMeters, 0.06f, generatedTextures, normalCache, roughnessCache);
        snowMaterial = RaceMaterialFactory.CreateSurface("SnowMaterial", profile.snowTexturePrefix, new Color(0.93f, 0.96f, 1f), profile.groundTileMeters, 0.12f, generatedTextures, normalCache, roughnessCache);
        shoulderMaterial = RaceMaterialFactory.CreateSurface("ShoulderMaterial", profile.snowCoveredGround ? profile.snowTexturePrefix : profile.groundTexturePrefix, profile.shoulderTint, profile.groundTileMeters, 0.08f, generatedTextures, normalCache, roughnessCache);
        curbMaterial = RaceMaterialFactory.CreateLit("CurbMaterial", profile.curbPrimary, 0.18f);
        curbAlternateMaterial = RaceMaterialFactory.CreateLit("CurbAlternateMaterial", profile.curbSecondary, 0.18f);
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

        generatedAssets.AddRange(new Object[]
        {
            roadMaterial, groundMaterial, snowMaterial, shoulderMaterial, curbMaterial, curbAlternateMaterial,
            stoneMaterial, woodMaterial, metalMaterial, bannerMaterial, darkMaterial, glassMaterial,
            foliageMaterial, rockMaterial, warmStoneMaterial, roofMaterial, seaMaterial, sailMaterial, lampMaterial, checks
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
        terrain.Build(generatedRoot.transform, profile, Samples, groundMaterial, snowMaterial, track.roadWidth * 0.5f, profile.seed + StableId(track.id ?? string.Empty));
        generatedAssets.Add(terrain.TerrainObject.GetComponent<MeshFilter>().sharedMesh);
    }

    private void CreateSampledPath()
    {
        for (int i = 0; i < sampleCount; i++) Samples.Add(CatmullRom(i / (float)sampleCount));
        for (int i = 0; i < sampleCount; i++)
        {
            Vector3 previous = Samples[(i - 1 + sampleCount) % sampleCount];
            Vector3 next = Samples[(i + 1) % sampleCount];
            Vector3 tangent = (next - previous).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, tangent).normalized;
            if (right.sqrMagnitude < 0.001f) right = Vector3.right;
            Vector3 normal = Vector3.Cross(tangent, right).normalized;
            right = Vector3.Cross(normal, tangent).normalized;
            Tangents.Add(tangent);
            rights.Add(right);
            normals.Add(normal);
            if (i > 0)
            {
                float distance = Vector3.Distance(Samples[i - 1], Samples[i]);
                distances.Add(distances.Count == 0 ? distance : distances[distances.Count - 1] + distance);
                TrackLength += distance;
            }
        }
        TrackLength += Vector3.Distance(Samples[sampleCount - 1], Samples[0]);
        distances.Add(TrackLength);
    }

    private Vector3 CatmullRom(float t)
    {
        int count = track.waypoints.Length;
        float scaled = t * count;
        int i = Mathf.FloorToInt(scaled) % count;
        float u = scaled - Mathf.FloorToInt(scaled);
        Vector3 p0 = track.waypoints[(i - 1 + count) % count];
        Vector3 p1 = track.waypoints[i];
        Vector3 p2 = track.waypoints[(i + 1) % count];
        Vector3 p3 = track.waypoints[(i + 2) % count];
        return 0.5f * ((2f * p1) + (-p0 + p2) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u * u + (-p0 + 3f * p1 - 3f * p2 + p3) * u * u * u);
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
            Vector3 leftRoad = point - right * half + normal * 0.045f;
            Vector3 rightRoad = point + right * half + normal * 0.045f;
            Vector3 leftShoulder = point - right * (half + shoulder) + normal * -0.015f;
            Vector3 rightShoulder = point + right * (half + shoulder) + normal * -0.015f;
            Vector3 leftGround = new Vector3(leftShoulder.x, terrain.SampleHeight(leftShoulder) - 0.08f, leftShoulder.z);
            Vector3 rightGround = new Vector3(rightShoulder.x, terrain.SampleHeight(rightShoulder) - 0.08f, rightShoulder.z);
            Vector3 nl = next - nextRight * half + nextNormal * 0.045f;
            Vector3 nr = next + nextRight * half + nextNormal * 0.045f;
            Vector3 nls = next - nextRight * (half + shoulder) + nextNormal * -0.015f;
            Vector3 nrs = next + nextRight * (half + shoulder) + nextNormal * -0.015f;
            Vector3 nlg = new Vector3(nls.x, terrain.SampleHeight(nls) - 0.08f, nls.z);
            Vector3 nrg = new Vector3(nrs.x, terrain.SampleHeight(nrs) - 0.08f, nrs.z);
            float segmentLength = Vector3.Distance(point, next);
            float distance = distances[i];
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
                    Vector3 top = point + right * side * (half + shoulder + profile.barrierOffset) + normal * 0.02f;
                    Vector3 nextTop = next + nextRight * side * (half + shoulder + profile.barrierOffset) + nextNormal * 0.02f;
                    Vector3 shoulderTop = point + right * side * (half + shoulder) + normal * -0.015f;
                    Vector3 nextShoulderTop = next + nextRight * side * (half + shoulder) + nextNormal * -0.015f;
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
        TrackMeshBuilder.CreateObject("Road", mesh, materials, generatedRoot.transform, true, true);
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
                Vector3 position = center + right * side * (half + profile.curbWidth * 0.5f) + normal * (profile.curbHeight * 0.5f + 0.035f);
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
                Vector3 a = point + right * side * offset + normal * 0.02f;
                Vector3 b = nextPoint + nextRight * side * offset + nextNormal * 0.02f;
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
        root.transform.SetPositionAndRotation(point + normal * 0.05f, rotation);
        for (int side = -1; side <= 1; side += 2)
        {
            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillar.name = "StartPillar";
            pillar.transform.SetParent(root.transform, false);
            pillar.transform.localPosition = new Vector3(side * (track.roadWidth * 0.5f + 0.62f), 2.435f, 0f);
            pillar.transform.localScale = new Vector3(0.34f, 5f, 0.34f);
            pillar.GetComponent<MeshRenderer>().sharedMaterial = woodMaterial;
            pillar.isStatic = true;
        }
        GameObject banner = GameObject.CreatePrimitive(PrimitiveType.Cube);
        banner.name = "StartBanner";
        banner.transform.SetParent(root.transform, false);
        banner.transform.localPosition = new Vector3(0f, 4.82f, 0f);
        banner.transform.localScale = new Vector3(track.roadWidth + 1.5f, 0.82f, 0.22f);
        banner.GetComponent<MeshRenderer>().sharedMaterial = bannerMaterial;
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

        for (int i = 0; i < 16; i++)
        {
            GameObject tile = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tile.name = "StartLineTile";
            tile.transform.SetParent(root.transform, false);
            float width = track.roadWidth * 0.96f / 16f;
            tile.transform.localPosition = new Vector3(-track.roadWidth * 0.48f + (i + 0.5f) * width, 0.0075f, 0f);
            tile.transform.localScale = new Vector3(width, 0.025f, 0.7f);
            tile.GetComponent<MeshRenderer>().sharedMaterial = i % 2 == 0 ? bannerMaterial : darkMaterial;
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
            checkpoint.transform.SetPositionAndRotation(Samples[sample] + normal * 0.06f, rotation);
            Checkpoint component = checkpoint.GetComponent<Checkpoint>();
            component.index = i;
            SphereCollider legacyTrigger = checkpoint.GetComponent<SphereCollider>();
            if (legacyTrigger != null) legacyTrigger.enabled = false;
            BoxCollider trigger = checkpoint.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, 3f, 0f);
            trigger.size = new Vector3(track.roadWidth + profile.shoulderWidth * 2f, 6f, 1.8f);

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
        TrackSceneryBuilder.Build(generatedRoot.transform, profile, Samples, terrain, random, track.roadWidth * 0.5f, snowMaterial, rockMaterial, stoneMaterial, warmStoneMaterial, roofMaterial, glassMaterial, woodMaterial, foliageMaterial, sailMaterial, metalMaterial, lampMaterial, seaMaterial, groundMaterial);
        foreach (Transform child in generatedRoot.transform)
        {
            if (child.name.StartsWith("Horizon_") || child.name == "Rocks" || child.name == "Sea" || child.name == "Harbor" || child.name == "DistantGround" || child.name == "Building_Fallback")
                RegisterRuntimeAssets(child.gameObject);
        }
        foreach (MeshFilter filter in generatedRoot.GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh != null && (filter.sharedMesh.name == "TreeTrunk" || filter.sharedMesh.name == "TreeCrown" || filter.sharedMesh.name == "SnowCap" || filter.sharedMesh.name == "StoneBuilding") && !generatedAssets.Contains(filter.sharedMesh))
                generatedAssets.Add(filter.sharedMesh);
        }
    }

    private void RegisterRuntimeAssets(GameObject root)
    {
        foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh != null && filter.sharedMesh.name != "Cube" && !generatedAssets.Contains(filter.sharedMesh)) generatedAssets.Add(filter.sharedMesh);
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
                    GameObject instance = Object.Instantiate(sign, position, rotation, generatedRoot.transform);
                    TrackSceneryBuilder.GroundInstance(instance, position.y);
                    instance.name = "RoadSign";
                    TrackSceneryBuilder.SetStatic(instance);
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
                    GameObject instance = Object.Instantiate(cart, position, Quaternion.LookRotation(Tangents[i], Vector3.up), generatedRoot.transform);
                    TrackSceneryBuilder.GroundInstance(instance, position.y);
                    instance.name = "HorseCart";
                    TrackSceneryBuilder.SetStatic(instance);
                }
            }
        }
    }

    public Vector3 PointAt(float progress)
    {
        if (Samples.Count == 0) return Vector3.zero;
        float scaled = Mathf.Repeat(progress, 0.9999f) * Samples.Count;
        int index = Mathf.FloorToInt(scaled) % Samples.Count;
        int next = (index + 1) % Samples.Count;
        return Vector3.Lerp(Samples[index], Samples[next], scaled - Mathf.FloorToInt(scaled));
    }

    public Vector3 TangentAt(float progress)
    {
        if (Tangents.Count == 0) return Vector3.forward;
        float scaled = Mathf.Repeat(progress, 0.9999f) * Tangents.Count;
        int index = Mathf.FloorToInt(scaled) % Tangents.Count;
        int next = (index + 1) % Tangents.Count;
        return Vector3.Lerp(Tangents[index], Tangents[next], scaled - Mathf.FloorToInt(scaled)).normalized;
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
    }

    private void OnDestroy()
    {
        foreach (Object asset in generatedAssets)
        {
            if (asset != null) Destroy(asset);
        }
        generatedAssets.Clear();
    }
}
