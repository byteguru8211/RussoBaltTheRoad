using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StartupCinematicController : MonoBehaviour
{
    private Camera cinematicCamera;
    private Canvas canvas;
    private Image fade;
    private Text byteguruText;
    private Text heckercatText;
    private Text presentsText;
    private Text titleText;
    private Text subtitleText;
    private Transform byteguruEmblem;
    private Transform heckercatEmblem;
    private Quaternion byteguruRestRotation;
    private Quaternion heckercatRestRotation;
    private Vector3 byteguruRestScale;
    private Vector3 heckercatRestScale;
    private Light keyLight;
    private float cinematicTime;
    private AudioSource logoAudio;
    private Button skipButton;
    private bool logoPlayed;
    private bool isLeaving;

    private void Awake()
    {
        if (GameManager.Instance == null)
            new GameObject("GameManager").AddComponent<GameManager>();
        if (HasArgument("-lanhost") || HasArgument("-lanjoin"))
        {
            enabled = false;
            return;
        }
        if (HasArgument("-skipIntro") || Application.isBatchMode)
        {
            SceneManager.LoadScene("MainMenu");
            enabled = false;
            return;
        }
        SetupScene();
        SetupUI();
        StartCoroutine(Play());
    }

    private bool HasArgument(string argument)
    {
        string[] args = System.Environment.GetCommandLineArgs();
        foreach (string value in args)
            if (value == argument) return true;
        return false;
    }

    private void SetupScene()
    {
        cinematicCamera = Camera.main;
        if (cinematicCamera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraObject.tag = "MainCamera";
            cinematicCamera = cameraObject.GetComponent<Camera>();
        }
        cinematicCamera.clearFlags = CameraClearFlags.SolidColor;
        cinematicCamera.backgroundColor = new Color(0.008f, 0.016f, 0.02f);
        cinematicCamera.fieldOfView = 46f;
        cinematicCamera.nearClipPlane = 0.05f;
        cinematicCamera.farClipPlane = 120f;
        cinematicCamera.allowHDR = true;
        cinematicCamera.transform.position = new Vector3(0f, 2.75f, 11.5f);
        cinematicCamera.transform.LookAt(new Vector3(0f, 2.45f, 0f));
        UniversalAdditionalCameraData cameraData = cinematicCamera.GetComponent<UniversalAdditionalCameraData>();
        if (cameraData == null) cameraData = cinematicCamera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        cameraData.renderPostProcessing = true;

        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Exponential;
        RenderSettings.fogDensity = 0.018f;
        RenderSettings.fogColor = new Color(0.018f, 0.032f, 0.04f);
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientSkyColor = new Color(0.12f, 0.18f, 0.20f);
        RenderSettings.ambientEquatorColor = new Color(0.05f, 0.07f, 0.08f);
        RenderSettings.ambientGroundColor = new Color(0.015f, 0.02f, 0.025f);

        GameObject stage = Resources.Load<GameObject>("Environment/ReleaseKit/Welcome_Workshop_Stage");
        if (stage != null) Instantiate(stage, Vector3.zero, stage.transform.rotation);
        GameObject byteguru = Resources.Load<GameObject>("Environment/ReleaseKit/Welcome_Byteguru_Emblem");
        GameObject heckercat = Resources.Load<GameObject>("Environment/ReleaseKit/Welcome_Heckercat_Emblem");
        if (byteguru != null)
        {
            byteguruEmblem = Instantiate(byteguru, new Vector3(-2.35f, 3.0f, 6.1f), Quaternion.Euler(90f, 0f, 0f)).transform;
            byteguruRestScale = byteguru.transform.localScale;
            byteguruEmblem.localScale = byteguruRestScale * 0.72f;
            byteguruRestRotation = byteguruEmblem.rotation;
        }
        if (heckercat != null)
        {
            heckercatEmblem = Instantiate(heckercat, new Vector3(2.35f, 3.0f, 6.1f), Quaternion.Euler(90f, 0f, 0f)).transform;
            heckercatRestScale = heckercat.transform.localScale;
            heckercatEmblem.localScale = heckercatRestScale * 0.72f;
            heckercatRestRotation = heckercatEmblem.rotation;
        }
        IlluminateEmblem(byteguruEmblem, -2.35f);
        IlluminateEmblem(heckercatEmblem, 2.35f);

        GameObject key = new GameObject("CinematicKey", typeof(Light));
        key.transform.position = new Vector3(-3.5f, 6.5f, 6f);
        key.transform.LookAt(new Vector3(0f, 2.4f, 0f));
        keyLight = key.GetComponent<Light>();
        keyLight.type = LightType.Spot;
        keyLight.color = new Color(1f, 0.68f, 0.36f);
        keyLight.intensity = 65f;
        keyLight.range = 28f;
        keyLight.spotAngle = 62f;
        keyLight.innerSpotAngle = 38f;
        keyLight.shadows = LightShadows.Soft;
        GameObject rim = new GameObject("CinematicRim", typeof(Light));
        rim.transform.position = new Vector3(4.5f, 5f, -1f);
        rim.transform.LookAt(new Vector3(0f, 2.5f, 0f));
        Light rimLight = rim.GetComponent<Light>();
        rimLight.type = LightType.Spot;
        rimLight.color = new Color(0.28f, 0.62f, 0.82f);
        rimLight.intensity = 48f;
        rimLight.range = 24f;
        rimLight.spotAngle = 70f;
        rimLight.shadows = LightShadows.None;

        VolumeProfile volumeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
        Bloom bloom = volumeProfile.Add<Bloom>(true);
        bloom.intensity.overrideState = true;
        bloom.intensity.value = 0.72f;
        bloom.threshold.overrideState = true;
        bloom.threshold.value = 0.82f;
        bloom.scatter.overrideState = true;
        bloom.scatter.value = 0.62f;
        Vignette vignette = volumeProfile.Add<Vignette>(true);
        vignette.intensity.overrideState = true;
        vignette.intensity.value = 0.42f;
        vignette.smoothness.overrideState = true;
        vignette.smoothness.value = 0.5f;
        GameObject volumeObject = new GameObject("CinematicVolume", typeof(Volume));
        Volume volume = volumeObject.GetComponent<Volume>();
        volume.sharedProfile = volumeProfile;
        volume.isGlobal = true;
        volume.priority = 50f;
        CreateMotes();
        AudioClip chime = Resources.Load<AudioClip>("Audio/logo_chime_1912");
        if (chime != null)
        {
            logoAudio = gameObject.AddComponent<AudioSource>();
            logoAudio.clip = chime;
            logoAudio.playOnAwake = false;
            logoAudio.loop = false;
            logoAudio.spatialBlend = 0f;
            logoAudio.ignoreListenerPause = true;
            logoAudio.volume = GameManager.Instance != null ? Mathf.Clamp01(GameManager.Instance.Settings.musicVolume) : 0.7f;
        }
    }

    private static void IlluminateEmblem(Transform emblem, float x)
    {
        if (emblem == null) return;
        foreach (MeshRenderer renderer in emblem.GetComponentsInChildren<MeshRenderer>())
        {
            Material[] source = renderer.sharedMaterials;
            Material[] materials = new Material[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                if (source[i] == null) continue;
                materials[i] = new Material(source[i]);
                materials[i].EnableKeyword("_EMISSION");
                materials[i].SetColor("_EmissionColor", new Color(0.34f, 0.20f, 0.06f));
                materials[i].globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            renderer.sharedMaterials = materials;
        }
        GameObject lightObject = new GameObject("EmblemLight", typeof(Light));
        lightObject.transform.position = new Vector3(x, 3.5f, 3.4f);
        Light light = lightObject.GetComponent<Light>();
        light.type = LightType.Point;
        light.color = new Color(1f, 0.62f, 0.28f);
        light.intensity = 5.5f;
        light.range = 7f;
        light.shadows = LightShadows.None;
    }

    private void CreateMotes()
    {
        Texture2D smoke = Resources.Load<Texture2D>("Textures/particle_smoke");
        Material material = RaceMaterialFactory.CreateParticle("CinematicMotes", new Color(1f, 0.72f, 0.38f, 0.22f), smoke);
        GameObject systemObject = new GameObject("CinematicMotes", typeof(ParticleSystem));
        ParticleSystem system = systemObject.GetComponent<ParticleSystem>();
        ParticleSystem.MainModule main = system.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 70;
        main.startLifetime = new ParticleSystem.MinMaxCurve(4f, 8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.03f, 0.12f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.012f, 0.035f);
        main.startColor = new Color(1f, 0.72f, 0.38f, 0.20f);
        main.gravityModifier = -0.01f;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(12f, 5f, 6f);
        shape.position = new Vector3(0f, 2.5f, 0f);
        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 9f;
        system.GetComponent<ParticleSystemRenderer>().sharedMaterial = material;
        system.GetComponent<ParticleSystemRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private void SetupUI()
    {
        canvas = UIUtils.CreateCanvas("StartupCanvas", 100);
        fade = UIUtils.Panel(canvas.transform, "Fade", Color.black, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, false);
        byteguruText = UIUtils.Text(canvas.transform, "ByteguruGames", "ByteguruGames", 42, UITheme.Cream, TextAnchor.MiddleCenter, new Vector2(0.18f, 0.70f), new Vector2(0.82f, 0.79f), Vector2.zero, Vector2.zero, UITheme.TitleFont);
        heckercatText = UIUtils.Text(canvas.transform, "HeckercatGames", "HeckercatGames", 42, UITheme.Cream, TextAnchor.MiddleCenter, new Vector2(0.18f, 0.19f), new Vector2(0.82f, 0.28f), Vector2.zero, Vector2.zero, UITheme.TitleFont);
        presentsText = UIUtils.Text(canvas.transform, "Presents", "P R E S E N T S", 18, UITheme.Brass, TextAnchor.MiddleCenter, new Vector2(0.30f, 0.36f), new Vector2(0.70f, 0.41f), Vector2.zero, Vector2.zero);
        titleText = UIUtils.Text(canvas.transform, "Title", "RUSSO-BALT\nTHE ROAD", 64, UITheme.Cream, TextAnchor.MiddleCenter, new Vector2(0.16f, 0.40f), new Vector2(0.84f, 0.62f), Vector2.zero, Vector2.zero, UITheme.TitleFont);
        subtitleText = UIUtils.Text(canvas.transform, "Subtitle", "1909—1914  ·  ИСТОРИЯ СКОРОСТИ", 18, UITheme.Brass, TextAnchor.MiddleCenter, new Vector2(0.20f, 0.32f), new Vector2(0.80f, 0.37f), Vector2.zero, Vector2.zero);
        UIUtils.Text(canvas.transform, "Skip", "ENTER / A / ТАП — ПРОПУСТИТЬ", 12, UITheme.Muted, TextAnchor.MiddleCenter, new Vector2(0.35f, 0.055f), new Vector2(0.65f, 0.085f), Vector2.zero, Vector2.zero);
        Image skipHit = UIUtils.Panel(canvas.transform, "SkipInput", new Color(0f, 0f, 0f, 0.001f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, true);
        skipButton = skipHit.gameObject.AddComponent<Button>();
        skipButton.targetGraphic = skipHit;
        ColorBlock skipColors = skipButton.colors;
        skipColors.normalColor = new Color(0f, 0f, 0f, 0f);
        skipColors.highlightedColor = new Color(0f, 0f, 0f, 0f);
        skipColors.pressedColor = new Color(0f, 0f, 0f, 0f);
        skipColors.selectedColor = new Color(0f, 0f, 0f, 0f);
        skipColors.fadeDuration = 0f;
        skipButton.colors = skipColors;
        skipButton.onClick.AddListener(() =>
        {
            if (cinematicTime >= 1f) LeaveCinematic();
        });
        SetAlpha(byteguruText, 0f);
        SetAlpha(heckercatText, 0f);
        SetAlpha(presentsText, 0f);
        SetAlpha(titleText, 0f);
        SetAlpha(subtitleText, 0f);
    }

    private IEnumerator Play()
    {
        while (cinematicTime < 10f)
        {
            cinematicTime += Time.unscaledDeltaTime;
            ApplyFrame(cinematicTime);
            yield return null;
        }
        LeaveCinematic();
    }

    private void ApplyFrame(float t)
    {
        if (!logoPlayed && t >= 0.85f)
        {
            logoPlayed = true;
            if (logoAudio != null) logoAudio.Play();
        }
        float dolly = SmoothStep(0f, 8.5f, t);
        cinematicCamera.transform.position = new Vector3(Mathf.Sin(t * 0.22f) * 0.22f, 2.75f - dolly * 0.18f, 11.5f - dolly * 2.4f);
        cinematicCamera.transform.LookAt(new Vector3(0f, 2.45f, 0f));
        keyLight.intensity = 58f + Mathf.Sin(t * 0.7f) * 8f;
        if (byteguruEmblem != null)
        {
            float scale = Mathf.Lerp(0.05f, 0.72f, SmoothStep(0.8f, 2.4f, t));
            byteguruEmblem.localScale = byteguruRestScale * scale;
            byteguruEmblem.rotation = Quaternion.Euler(0f, Mathf.Sin(t * 0.45f) * 0.08f, 0f) * byteguruRestRotation;
        }
        if (heckercatEmblem != null)
        {
            float scale = Mathf.Lerp(0.05f, 0.72f, SmoothStep(2.8f, 4.4f, t));
            heckercatEmblem.localScale = heckercatRestScale * scale;
            heckercatEmblem.rotation = Quaternion.Euler(0f, -Mathf.Sin(t * 0.45f) * 0.08f, 0f) * heckercatRestRotation;
        }
        SetAlpha(byteguruText, SmoothStep(1.2f, 2.3f, t) * (1f - SmoothStep(7.2f, 8.2f, t)));
        SetAlpha(heckercatText, SmoothStep(3.2f, 4.3f, t) * (1f - SmoothStep(7.2f, 8.2f, t)));
        SetAlpha(presentsText, SmoothStep(4.7f, 5.5f, t) * (1f - SmoothStep(6.3f, 7f, t)));
        SetAlpha(titleText, SmoothStep(6.1f, 7.2f, t));
        SetAlpha(subtitleText, SmoothStep(6.8f, 7.8f, t));
        float fadeAlpha = t < 1.2f ? 1f - SmoothStep(0f, 1.2f, t) : SmoothStep(8.7f, 10f, t);
        fade.color = new Color(0f, 0f, 0f, fadeAlpha);
    }

    private void Update()
    {
        if (cinematicTime < 1f || isLeaving) return;
        UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
        UnityEngine.InputSystem.Gamepad gamepad = UnityEngine.InputSystem.Gamepad.current;
        bool skip = keyboard != null && (keyboard.enterKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame)
            || gamepad != null && gamepad.buttonSouth.wasPressedThisFrame;
        if (skip) LeaveCinematic();
    }

    private void LeaveCinematic()
    {
        if (isLeaving) return;
        isLeaving = true;
        if (logoAudio != null) logoAudio.Stop();
        SceneManager.LoadScene("MainMenu");
    }

    private static float SmoothStep(float from, float to, float value)
    {
        if (Mathf.Abs(to - from) < 0.0001f) return to;
        float t = Mathf.Clamp01((value - from) / (to - from));
        return t * t * (3f - 2f * t);
    }

    private static void SetAlpha(Text text, float alpha)
    {
        if (text == null) return;
        Color color = text.color;
        color.a = Mathf.Clamp01(alpha);
        text.color = color;
    }
}
