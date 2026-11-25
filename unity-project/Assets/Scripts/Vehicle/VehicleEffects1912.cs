using UnityEngine;

public class VehicleEffects1912 : MonoBehaviour
{
    public enum SurfaceKind { None, Dirt, Snow, Stone }

    [Header("Vehicle")]
    public VehicleController1912 vehicle;

    [Header("Optional model sockets")]
    public Transform dustSocket;
    public Transform leftExhaustSocket;
    public Transform rightExhaustSocket;
    public Transform leftHeadlampSocket;
    public Transform rightHeadlampSocket;

    [Header("Headlamps")]
    public bool automaticHeadlamps = true;
    public float headlampIntensity = 3.2f;
    public float headlampRange = 32f;
    public LayerMask surfaceMask = ~0;

    private ParticleSystem dust;
    private ParticleSystem leftExhaust;
    private ParticleSystem rightExhaust;
    private Material dustMaterial;
    private Material exhaustMaterial;
    private Texture2D generatedSmokeTexture;
    private Light leftHeadlamp;
    private Light rightHeadlamp;
    private SurfaceKind surface = SurfaceKind.None;
    private float surfaceRefreshTimer;

    private void Awake()
    {
        if (vehicle == null) vehicle = GetComponent<VehicleController1912>();
        Texture2D dustTexture = Resources.Load<Texture2D>("Textures/particle_dust");
        Texture2D smokeTexture = Resources.Load<Texture2D>("Textures/particle_smoke");
        if (smokeTexture == null)
        {
            generatedSmokeTexture = CreateSmokeTexture();
            smokeTexture = generatedSmokeTexture;
        }
        if (dustTexture == null) dustTexture = smokeTexture;

        dustMaterial = RaceMaterialFactory.CreateParticle("VehicleDust", Color.white, dustTexture);
        exhaustMaterial = RaceMaterialFactory.CreateParticle("VehicleExhaust", Color.white, smokeTexture);
        dustMaterial.SetFloat("_SoftParticlesEnabled", 1f);
        exhaustMaterial.SetFloat("_SoftParticlesEnabled", 1f);

        dustSocket = ResolveSocket(dustSocket, "DustTrail", new Vector3(0f, 0.24f, -1.65f));
        leftExhaustSocket = ResolveSocket(leftExhaustSocket, "ExhaustSmoke_Left", new Vector3(-0.43f, 0.42f, -1.75f));
        rightExhaustSocket = ResolveSocket(rightExhaustSocket, "ExhaustSmoke_Right", new Vector3(0.43f, 0.42f, -1.75f));

        dust = CreateSystem("DustTrail", dustSocket, dustMaterial, true);
        leftExhaust = CreateSystem("ExhaustSmoke_Left", leftExhaustSocket, exhaustMaterial, false);
        rightExhaust = CreateSystem("ExhaustSmoke_Right", rightExhaustSocket, exhaustMaterial, false);
        CreateHeadlamp(leftHeadlampSocket, "Headlamp_Left", new Vector3(-0.52f, 0.78f, 2.02f), out leftHeadlamp);
        CreateHeadlamp(rightHeadlampSocket, "Headlamp_Right", new Vector3(0.52f, 0.78f, 2.02f), out rightHeadlamp);
    }

    private Transform ResolveSocket(Transform assigned, string name, Vector3 fallback)
    {
        if (assigned != null) return assigned;
        Transform existing = FindSocket(name);
        if (existing != null) return existing;
        GameObject socket = new GameObject(name);
        socket.transform.SetParent(transform, false);
        socket.transform.localPosition = fallback;
        return socket.transform;
    }

    private Transform FindSocket(string exactName)
    {
        foreach (Transform child in transform)
        {
            if (child.name == exactName) return child;
            Transform nested = child.Find(exactName);
            if (nested != null) return nested;
        }
        return null;
    }

    private ParticleSystem CreateSystem(string name, Transform socket, Material material, bool isDust)
    {
        socket.name = name;
        ParticleSystem system = socket.GetComponent<ParticleSystem>();
        if (system == null) system = socket.gameObject.AddComponent<ParticleSystem>();
        system.Stop();
        ParticleSystem.MainModule main = system.main;
        main.loop = true;
        main.startLifetime = isDust ? new ParticleSystem.MinMaxCurve(1.1f, 2.2f) : new ParticleSystem.MinMaxCurve(1.3f, 2.5f);
        main.startSpeed = isDust ? new ParticleSystem.MinMaxCurve(0.35f, 1.15f) : new ParticleSystem.MinMaxCurve(0.22f, 0.62f);
        main.startSize = isDust ? new ParticleSystem.MinMaxCurve(0.24f, 0.5f) : new ParticleSystem.MinMaxCurve(0.12f, 0.25f);
        main.startColor = isDust ? new Color(0.48f, 0.38f, 0.25f, 0.42f) : new Color(0.2f, 0.21f, 0.2f, 0.32f);
        main.gravityModifier = isDust ? 0.07f : -0.035f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = isDust ? 180 : 70;
        main.stopAction = ParticleSystemStopAction.None;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.playOnAwake = false;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = isDust ? 24f : 11f;
        shape.radius = isDust ? 0.68f : 0.035f;
        shape.length = isDust ? 0.22f : 0.08f;
        shape.rotation = new Vector3(isDust ? -78f : -68f, 0f, 0f);

        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 0f;
        ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
        color.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(new[]
        {
            new GradientColorKey(Color.white, 0f),
            new GradientColorKey(isDust ? new Color(0.94f, 0.9f, 0.84f) : new Color(0.72f, 0.74f, 0.73f), 1f)
        }, new[]
        {
            new GradientAlphaKey(0f, 0f),
            new GradientAlphaKey(isDust ? 0.52f : 0.4f, 0.14f),
            new GradientAlphaKey(isDust ? 0.25f : 0.2f, 0.58f),
            new GradientAlphaKey(0f, 1f)
        });
        color.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(new Keyframe(0f, isDust ? 0.38f : 0.42f), new Keyframe(0.25f, 1f), new Keyframe(1f, isDust ? 2.6f : 2.15f));
        size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);
        ParticleSystem.RotationOverLifetimeModule rotation = system.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = isDust ? new ParticleSystem.MinMaxCurve(-22f, 22f) : new ParticleSystem.MinMaxCurve(-12f, 12f);
        ParticleSystem.NoiseModule noise = system.noise;
        noise.enabled = true;
        noise.strength = isDust ? 0.16f : 0.1f;
        noise.frequency = isDust ? 0.45f : 0.28f;

        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sortMode = ParticleSystemSortMode.YoungestInFront;
        renderer.minParticleSize = 0.01f;
        renderer.maxParticleSize = 4f;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        system.Play();
        return system;
    }

    private void CreateHeadlamp(Transform assigned, string name, Vector3 fallback, out Light lamp)
    {
        Transform socket = assigned != null ? assigned : FindSocket(name);
        if (socket == null)
        {
            GameObject socketObject = new GameObject(name);
            socketObject.transform.SetParent(transform, false);
            socketObject.transform.localPosition = fallback;
            socket = socketObject.transform;
        }
        lamp = socket.GetComponent<Light>();
        if (lamp == null) lamp = socket.gameObject.AddComponent<Light>();
        lamp.type = LightType.Spot;
        lamp.color = new Color(1f, 0.73f, 0.43f);
        lamp.intensity = headlampIntensity;
        lamp.range = headlampRange;
        lamp.spotAngle = 64f;
        lamp.innerSpotAngle = 24f;
        lamp.shadows = LightShadows.None;
        lamp.enabled = false;
    }

    private void Update()
    {
        if (vehicle == null || dust == null || leftExhaust == null || rightExhaust == null) return;
        surfaceRefreshTimer -= Time.deltaTime;
        if (surfaceRefreshTimer <= 0f)
        {
            surfaceRefreshTimer = 0.14f;
            surface = DetectSurface();
        }

        float speed = Mathf.Clamp01(vehicle.SpeedKmh / 55f);
        float slip = GetWheelSlip();
        float surfaceDust = surface == SurfaceKind.Stone ? 0.28f : surface == SurfaceKind.Snow ? 1.15f : 1f;
        float dustRate = surface != SurfaceKind.None && vehicle.SpeedKmh > 3f ? (5f + speed * 34f + slip * 22f) * surfaceDust : 0f;
        ParticleSystem.EmissionModule dustEmission = dust.emission;
        dustEmission.rateOverTime = dustRate;
        ParticleSystem.MainModule dustMain = dust.main;
        float sizeMultiplier = surface == SurfaceKind.Snow ? 1.35f : 1f;
        dustMain.startSize = new ParticleSystem.MinMaxCurve((0.24f + speed * 0.34f) * sizeMultiplier, (0.48f + speed * 0.58f) * sizeMultiplier);
        dustMain.startColor = SurfaceColor(surface);

        float exhaustRate = vehicle.engineRunning ? 2.5f + vehicle.Throttle * 11f + Mathf.Clamp01(vehicle.rpm / 2400f) * 3f : 0f;
        ParticleSystem.EmissionModule leftEmission = leftExhaust.emission;
        ParticleSystem.EmissionModule rightEmission = rightExhaust.emission;
        leftEmission.rateOverTime = exhaustRate * 0.5f;
        rightEmission.rateOverTime = exhaustRate * 0.5f;

        bool lampsOn = automaticHeadlamps && vehicle.engineRunning;
        if (leftHeadlamp != null) leftHeadlamp.enabled = lampsOn;
        if (rightHeadlamp != null) rightHeadlamp.enabled = lampsOn;
    }

    private float GetWheelSlip()
    {
        if (vehicle.rearLeft == null && vehicle.rearRight == null) return 0f;
        float slip = 0f;
        int count = 0;
        if (vehicle.rearLeft != null && vehicle.rearLeft.GetGroundHit(out WheelHit left))
        {
            slip += Mathf.Max(Mathf.Abs(left.forwardSlip), Mathf.Abs(left.sidewaysSlip));
            count++;
        }
        if (vehicle.rearRight != null && vehicle.rearRight.GetGroundHit(out WheelHit right))
        {
            slip += Mathf.Max(Mathf.Abs(right.forwardSlip), Mathf.Abs(right.sidewaysSlip));
            count++;
        }
        return count > 0 ? Mathf.Clamp01(slip / count) : 0f;
    }

    private SurfaceKind DetectSurface()
    {
        if (vehicle.rearLeft != null && vehicle.rearLeft.GetGroundHit(out WheelHit left)) return ClassifySurface(left.collider, left.point);
        if (vehicle.rearRight != null && vehicle.rearRight.GetGroundHit(out WheelHit right)) return ClassifySurface(right.collider, right.point);
        Vector3 origin = transform.position + Vector3.up * 0.8f;
        RaycastHit[] hits = Physics.RaycastAll(origin, Vector3.down, 3.5f, surfaceMask, QueryTriggerInteraction.Ignore);
        Rigidbody ownBody = GetComponent<Rigidbody>();
        foreach (RaycastHit hit in hits)
        {
            if (ownBody != null && hit.rigidbody == ownBody) continue;
            SurfaceKind kind = ClassifySurface(hit.collider, hit.point);
            if (kind != SurfaceKind.None) return kind;
        }
        return SurfaceKind.None;
    }

    private static SurfaceKind ClassifySurface(Collider collider, Vector3 worldPoint)
    {
        TrackSurface1912 trackSurface = collider.GetComponent<TrackSurface1912>();
        if (trackSurface != null && trackSurface.IsSnowAt(worldPoint)) return SurfaceKind.Snow;
        string objectName = collider.gameObject.name.ToLowerInvariant();
        MeshRenderer renderer = collider.GetComponent<MeshRenderer>();
        Terrain terrain = collider.GetComponent<Terrain>();
        PhysicsMaterial physicsMaterial = collider.sharedMaterial;
        Material material = renderer != null ? renderer.sharedMaterial : null;
        if (material == null && terrain != null) material = terrain.materialTemplate;
        if (physicsMaterial != null)
        {
            string physicsName = physicsMaterial.name.ToLowerInvariant();
            if (physicsName.Contains("snow")) return SurfaceKind.Snow;
            if (physicsName.Contains("stone") || physicsName.Contains("cobble")) return SurfaceKind.Stone;
        }
        string materialName = material != null ? material.name.ToLowerInvariant() : "";
        string textureName = material != null && material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null ? material.GetTexture("_BaseMap").name.ToLowerInvariant() : "";
        if (objectName.Contains("snow") || materialName.Contains("snow") || textureName.Contains("snow")) return SurfaceKind.Snow;
        if (objectName.Contains("cobble") || objectName.Contains("stone") || objectName.Contains("asphalt") || materialName.Contains("stone") || materialName.Contains("cobble") || textureName.Contains("cobble")) return SurfaceKind.Stone;
        if (objectName.Contains("terrain") || objectName.Contains("road") || materialName.Contains("ground") || materialName.Contains("dirt")) return SurfaceKind.Dirt;
        return SurfaceKind.Dirt;
    }

    private static Color SurfaceColor(SurfaceKind kind)
    {
        switch (kind)
        {
            case SurfaceKind.Snow: return new Color(0.96f, 0.98f, 1f, 0.48f);
            case SurfaceKind.Stone: return new Color(0.72f, 0.68f, 0.6f, 0.24f);
            case SurfaceKind.None: return new Color(0.48f, 0.38f, 0.25f, 0f);
            default: return new Color(1.28f, 1.12f, 0.92f, 0.42f);
        }
    }

    private static Texture2D CreateSmokeTexture()
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true, true)
        {
            name = "GeneratedSoftSmoke",
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };
        Color[] pixels = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float nx = (x + 0.5f) / size * 2f - 1f;
                float ny = (y + 0.5f) / size * 2f - 1f;
                float radius = Mathf.Sqrt(nx * nx + ny * ny);
                float wisps = 0.82f + Mathf.Sin(x * 0.41f + y * 0.23f) * 0.09f + Mathf.Sin(y * 0.37f - x * 0.19f) * 0.09f;
                float alpha = Mathf.Clamp01(1f - radius) * Mathf.Clamp01(1f - radius) * wisps;
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }
        texture.SetPixels(pixels);
        texture.Apply(true, true);
        return texture;
    }

    private void OnDestroy()
    {
        if (dustMaterial != null) Destroy(dustMaterial);
        if (exhaustMaterial != null) Destroy(exhaustMaterial);
        if (generatedSmokeTexture != null) Destroy(generatedSmokeTexture);
    }
}
