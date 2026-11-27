using UnityEngine;

public class VehicleEffects1912 : MonoBehaviour
{
    public enum SurfaceKind { None, Dirt, Cobblestone, Snow, Ice, Grass, Stone }

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
    private Camera viewCamera;

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

        BoxCollider hull = GetComponent<BoxCollider>();
        float length = hull != null ? hull.size.z : 4.2f;
        float rear = -length * 0.43f;
        float rearAxle = vehicle != null && vehicle.rearLeft != null ? vehicle.rearLeft.transform.localPosition.z : rear;
        dustSocket = ResolveSocket(dustSocket, "DustTrail", new Vector3(0f, 0.16f, rearAxle));
        leftExhaustSocket = ResolveSocket(leftExhaustSocket, "ExhaustSmoke_Left", new Vector3(-0.40f, 0.38f, rear));
        rightExhaustSocket = ResolveSocket(rightExhaustSocket, "ExhaustSmoke_Right", new Vector3(0.40f, 0.38f, rear));

        dust = CreateSystem("DustTrail", dustSocket, dustMaterial, true);
        leftExhaust = CreateSystem("ExhaustSmoke_Left", leftExhaustSocket, exhaustMaterial, false);
        rightExhaust = CreateSystem("ExhaustSmoke_Right", rightExhaustSocket, exhaustMaterial, false);
        CreateHeadlamp(leftHeadlampSocket, "Headlamp_Left", new Vector3(-0.52f, 0.78f, length * 0.44f), out leftHeadlamp);
        CreateHeadlamp(rightHeadlampSocket, "Headlamp_Right", new Vector3(0.52f, 0.78f, length * 0.44f), out rightHeadlamp);
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
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
            if (child.name == exactName) return child;
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
        main.startLifetime = isDust ? new ParticleSystem.MinMaxCurve(0.45f, 0.95f) : new ParticleSystem.MinMaxCurve(0.42f, 0.88f);
        main.startSpeed = isDust ? new ParticleSystem.MinMaxCurve(0.18f, 0.62f) : new ParticleSystem.MinMaxCurve(0.16f, 0.38f);
        main.startSize = isDust ? new ParticleSystem.MinMaxCurve(0.10f, 0.22f) : new ParticleSystem.MinMaxCurve(0.065f, 0.13f);
        main.startColor = isDust ? new Color(0.48f, 0.38f, 0.25f, 0.20f) : new Color(0.24f, 0.25f, 0.24f, 0.11f);
        main.gravityModifier = isDust ? 0.04f : -0.015f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = isDust ? 48 : 12;
        main.stopAction = ParticleSystemStopAction.None;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.playOnAwake = false;

        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = isDust ? 18f : 8f;
        shape.radius = isDust ? 0.24f : 0.018f;
        shape.length = isDust ? 0.12f : 0.04f;
        shape.rotation = new Vector3(-90f, 0f, 0f);

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
            new GradientAlphaKey(isDust ? 0.20f : 0.095f, 0.16f),
            new GradientAlphaKey(isDust ? 0.085f : 0.035f, 0.58f),
            new GradientAlphaKey(0f, 1f)
        });
        color.color = new ParticleSystem.MinMaxGradient(gradient);

        ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
        size.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve(new Keyframe(0f, isDust ? 0.42f : 0.48f), new Keyframe(0.3f, 1f), new Keyframe(1f, isDust ? 1.75f : 1.5f));
        size.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);
        ParticleSystem.RotationOverLifetimeModule rotation = system.rotationOverLifetime;
        rotation.enabled = true;
        rotation.z = new ParticleSystem.MinMaxCurve(-14f, 14f);
        ParticleSystem.NoiseModule noise = system.noise;
        noise.enabled = true;
        noise.strength = isDust ? 0.06f : 0.025f;
        noise.frequency = isDust ? 0.32f : 0.22f;

        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.alignment = ParticleSystemRenderSpace.View;
        renderer.sortMode = ParticleSystemSortMode.YoungestInFront;
        renderer.minParticleSize = 0.01f;
        renderer.maxParticleSize = 1.4f;
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
        if (viewCamera == null) viewCamera = Camera.main;
        surfaceRefreshTimer -= Time.deltaTime;
        if (surfaceRefreshTimer <= 0f)
        {
            surfaceRefreshTimer = 0.14f;
            surface = DetectSurface();
        }

        float speed = Mathf.Clamp01(Mathf.Abs(vehicle.SpeedKmh) / 55f);
        float slip = GetWheelSlip();
        float cameraDistance = viewCamera != null ? Vector3.Distance(viewCamera.transform.position, dustSocket.position) : 20f;
        float viewFadeT = Mathf.Clamp01((cameraDistance - 2.2f) / (6.5f - 2.2f));
        float viewFade = viewFadeT * viewFadeT * (3f - 2f * viewFadeT);
        float distanceFade = vehicle.isPlayer ? 1f : Mathf.Clamp01((90f - cameraDistance) / 30f);
        float surfaceDust = surface switch
        {
            SurfaceKind.Snow => 0.55f,
            SurfaceKind.Ice => 0.08f,
            SurfaceKind.Cobblestone => 0.22f,
            SurfaceKind.Stone => 0.25f,
            SurfaceKind.Grass => 0.8f,
            SurfaceKind.None => 0f,
            _ => 1f
        };
        float dustRate = surface != SurfaceKind.None && Mathf.Abs(vehicle.SpeedKmh) > 4f
            ? (2.5f + speed * 10f + slip * 7f) * surfaceDust * viewFade * distanceFade
            : 0f;
        ParticleSystem.EmissionModule dustEmission = dust.emission;
        dustEmission.rateOverTime = dustRate;
        ParticleSystem.MainModule dustMain = dust.main;
        float sizeMultiplier = surface == SurfaceKind.Snow ? 1.12f : 1f;
        dustMain.startSize = new ParticleSystem.MinMaxCurve((0.10f + speed * 0.10f) * sizeMultiplier, (0.20f + speed * 0.18f) * sizeMultiplier);
        dustMain.startColor = SurfaceColor(surface);

        float load = Mathf.Clamp01(vehicle.rpm / Mathf.Max(1f, vehicle.rpmLimiter));
        float exhaustRate = vehicle.engineRunning ? (0.10f + vehicle.Throttle * 0.72f + load * 0.16f) * distanceFade : 0f;
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
        TrackSurface1912 trackSurface = collider != null ? collider.GetComponent<TrackSurface1912>() : null;
        if (trackSurface != null)
        {
            TrackSurface1912.SurfaceKind kind = trackSurface.GetSurfaceKindAt(worldPoint);
            return kind switch
            {
                TrackSurface1912.SurfaceKind.Snow => SurfaceKind.Snow,
                TrackSurface1912.SurfaceKind.Ice => SurfaceKind.Ice,
                TrackSurface1912.SurfaceKind.Cobblestone => SurfaceKind.Cobblestone,
                TrackSurface1912.SurfaceKind.Grass => SurfaceKind.Grass,
                TrackSurface1912.SurfaceKind.Stone => SurfaceKind.Stone,
                _ => SurfaceKind.Dirt
            };
        }
        string objectName = collider != null ? collider.gameObject.name.ToLowerInvariant() : "";
        MeshRenderer renderer = collider != null ? collider.GetComponent<MeshRenderer>() : null;
        PhysicsMaterial physicsMaterial = collider != null ? collider.sharedMaterial : null;
        Material material = renderer != null ? renderer.sharedMaterial : null;
        if (physicsMaterial != null)
        {
            string physicsName = physicsMaterial.name.ToLowerInvariant();
            if (physicsName.Contains("snow")) return SurfaceKind.Snow;
            if (physicsName.Contains("ice")) return SurfaceKind.Ice;
            if (physicsName.Contains("stone") || physicsName.Contains("cobble")) return SurfaceKind.Cobblestone;
        }
        string materialName = material != null ? material.name.ToLowerInvariant() : "";
        string textureName = material != null && material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null ? material.GetTexture("_BaseMap").name.ToLowerInvariant() : "";
        if (objectName.Contains("snow") || materialName.Contains("snow") || textureName.Contains("snow")) return SurfaceKind.Snow;
        if (objectName.Contains("cobble") || objectName.Contains("stone") || materialName.Contains("stone") || materialName.Contains("cobble") || textureName.Contains("cobble")) return SurfaceKind.Cobblestone;
        if (objectName.Contains("terrain") || objectName.Contains("grass") || materialName.Contains("ground") || materialName.Contains("dirt")) return SurfaceKind.Dirt;
        return SurfaceKind.None;
    }

    private static Color SurfaceColor(SurfaceKind kind)
    {
        switch (kind)
        {
            case SurfaceKind.Snow: return new Color(0.96f, 0.98f, 1f, 0.24f);
            case SurfaceKind.Cobblestone:
            case SurfaceKind.Stone: return new Color(0.72f, 0.68f, 0.6f, 0.12f);
            case SurfaceKind.None: return new Color(0.48f, 0.38f, 0.25f, 0f);
            default: return new Color(1.28f, 1.12f, 0.92f, 0.20f);
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
