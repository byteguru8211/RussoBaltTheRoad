using UnityEngine;

public class VehicleEffects1912 : MonoBehaviour
{
    public VehicleController1912 vehicle;
    private ParticleSystem dust;
    private ParticleSystem exhaust;
    private Material dustMaterial;
    private Material exhaustMaterial;

    private void Awake()
    {
        if (vehicle == null) vehicle = GetComponent<VehicleController1912>();
        Texture2D particle = Resources.Load<Texture2D>("Textures/particle_dust");
        dustMaterial = CreateParticleMaterial(new Color(0.48f, 0.38f, 0.25f, 0.34f), particle);
        exhaustMaterial = CreateParticleMaterial(new Color(0.18f, 0.19f, 0.18f, 0.24f), particle);
        dust = CreateSystem("DustTrail", new Vector3(0f, 0.24f, -1.65f), dustMaterial, new Vector2(0.35f, 0.85f), 1.4f, 1.8f);
        exhaust = CreateSystem("ExhaustSmoke", new Vector3(0.43f, 0.42f, -1.75f), exhaustMaterial, new Vector2(0.10f, 0.28f), 0.7f, 0.4f);
    }

    private Material CreateParticleMaterial(Color color, Texture2D texture)
    {
        Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        material.SetColor("_BaseColor", color);
        if (texture != null) material.SetTexture("_BaseMap", texture);
        return material;
    }

    private ParticleSystem CreateSystem(string name, Vector3 position, Material material, Vector2 size, float lifetime, float speed)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = position;
        ParticleSystem system = go.AddComponent<ParticleSystem>();
        system.Stop();
        ParticleSystem.MainModule main = system.main;
        main.loop = true;
        main.startLifetime = lifetime;
        main.startSpeed = speed;
        main.startSize = size.x;
        main.startColor = material.GetColor("_BaseColor");
        main.gravityModifier = 0.04f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = 120;
        ParticleSystem.ShapeModule shape = system.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(0.9f, 0.08f, 0.25f);
        ParticleSystem.EmissionModule emission = system.emission;
        emission.rateOverTime = 0f;
        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        system.Play();
        return system;
    }

    private void Update()
    {
        if (vehicle == null || dust == null || exhaust == null) return;
        float speed = Mathf.Clamp01(vehicle.SpeedKmh / 55f);
        float dustRate = speed > 0.08f ? 8f + speed * 42f : 0f;
        ParticleSystem.EmissionModule dustEmission = dust.emission;
        dustEmission.rateOverTime = dustRate;
        ParticleSystem.MainModule dustMain = dust.main;
        dustMain.startSize = 0.25f + speed * 0.55f;
        float exhaustRate = vehicle.engineRunning ? 3f + vehicle.Throttle * 13f : 0f;
        ParticleSystem.EmissionModule exhaustEmission = exhaust.emission;
        exhaustEmission.rateOverTime = exhaustRate;
    }

    private void OnDestroy()
    {
        if (dustMaterial != null) Destroy(dustMaterial);
        if (exhaustMaterial != null) Destroy(exhaustMaterial);
    }
}
