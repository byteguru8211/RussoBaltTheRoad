using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public static class RaceMaterialFactory
{
    public static Material CreateLit(string name, Color color, float smoothness, float metallic = 0f)
    {
        Material template = Resources.Load<Material>("Effects/URPLit");
        Shader shader = template != null ? template.shader : Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        Material material = new Material(shader) { name = name };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        material.enableInstancing = true;
        return material;
    }

    public static Material CreateUnlit(string name, Color color)
    {
        Material template = Resources.Load<Material>("Effects/URPUnlit");
        Shader shader = template != null ? template.shader : Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        Material material = new Material(shader) { name = name };
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color")) material.SetColor("_Color", color);
        material.enableInstancing = true;
        return material;
    }

    public static Material CreateSurface(string name, string texturePrefix, Color tint, float tileMeters, float smoothness, List<Texture2D> generatedTextures, Dictionary<Texture2D, Texture2D> normalCache = null, Dictionary<Texture2D, Texture2D> roughnessCache = null)
    {
        Material material = CreateLit(name, tint, smoothness);
        Texture2D baseMap = Resources.Load<Texture2D>($"Textures/{texturePrefix}_basecolor");
        Texture2D normalMap = Resources.Load<Texture2D>($"Textures/{texturePrefix}_normal");
        Texture2D roughnessMap = Resources.Load<Texture2D>($"Textures/{texturePrefix}_roughness");
        if (baseMap != null)
        {
            material.SetTexture("_BaseMap", baseMap);
            material.EnableKeyword("_BASEMAP");
        }
        if (normalMap != null)
        {
            material.SetTexture("_BumpMap", EnsureLinearCopy(normalMap, generatedTextures, normalCache));
            material.SetFloat("_BumpScale", 0.7f);
            material.EnableKeyword("_NORMALMAP");
        }
        if (roughnessMap != null)
        {
            Texture2D packed;
            if (roughnessCache != null && roughnessCache.TryGetValue(roughnessMap, out packed)) { }
            else
            {
                packed = PackRoughness(roughnessMap, generatedTextures);
                if (roughnessCache != null) roughnessCache.Add(roughnessMap, packed);
            }
            material.SetTexture("_MetallicGlossMap", packed);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 1f);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
        }
        SetWorldTiling(material, tileMeters);
        return material;
    }

    public static void SetWorldTiling(Material material, float metersPerTile)
    {
        Vector2 scale = new Vector2(1f / Mathf.Max(0.1f, metersPerTile), 1f / Mathf.Max(0.1f, metersPerTile));
        foreach (string property in new[] { "_BaseMap", "_BumpMap", "_MetallicGlossMap", "_EmissionMap", "_OcclusionMap" })
        {
            if (material.HasProperty(property)) material.SetTextureScale(property, scale);
        }
    }

    public static void MakeTransparent(Material material, Color color, int queue = 3000)
    {
        material.SetColor("_BaseColor", color);
        material.SetFloat("_Surface", 1f);
        material.SetFloat("_Blend", 0f);
        material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_SrcBlendAlpha", (float)BlendMode.SrcAlpha);
        material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite", 0f);
        material.SetFloat("_AlphaClip", 0f);
        material.SetFloat("_Cull", 0f);
        material.renderQueue = queue;
        material.SetOverrideTag("RenderType", "Transparent");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHAMODULATE_ON");
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
    }

    public static Material CreateParticle(string name, Color color, Texture2D texture)
    {
        Material template = Resources.Load<Material>("Effects/URPParticleUnlit");
        Shader shader = template != null ? template.shader : Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
        Material material = new Material(shader) { name = name };
        material.SetColor("_BaseColor", color);
        if (texture != null) material.SetTexture("_BaseMap", texture);
        MakeTransparent(material, color);
        material.SetFloat("_SoftParticlesEnabled", 1f);
        material.SetFloat("_SoftParticlesNearFadeDistance", 0.1f);
        material.SetFloat("_SoftParticlesFarFadeDistance", 1.5f);
        material.EnableKeyword("_SOFTPARTICLES_ON");
        return material;
    }

    private static Texture2D EnsureLinearCopy(Texture2D source, List<Texture2D> generatedTextures, Dictionary<Texture2D, Texture2D> cache)
    {
        if (cache != null && cache.TryGetValue(source, out Texture2D cached)) return cached;
        Texture2D copy = CopyTextureRaw(source);
        copy.name = source.name + "_LinearNormal";
        generatedTextures.Add(copy);
        if (cache != null) cache.Add(source, copy);
        return copy;
    }

    private static Texture2D CopyTextureRaw(Texture2D source)
    {
        Texture2D copy = new Texture2D(source.width, source.height, source.format, source.mipmapCount > 1, true)
        {
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Trilinear
        };
        Graphics.CopyTexture(source, copy);
        return copy;
    }

    private static Texture2D PackRoughness(Texture2D source, List<Texture2D> generatedTextures)
    {
        Texture2D raw = CopyTextureRaw(source);
        Color[] pixels = raw.GetPixels();
        UnityEngine.Object.DestroyImmediate(raw);
        Texture2D packed = new Texture2D(source.width, source.height, TextureFormat.RGBA32, true, true)
        {
            name = source.name + "_PackedRoughness",
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Trilinear
        };
        for (int i = 0; i < pixels.Length; i++)
        {
            float roughness = Mathf.Clamp01(pixels[i].r);
            pixels[i] = new Color(0f, roughness, roughness, 1f - roughness);
        }
        packed.SetPixels(pixels);
        packed.Apply(true, false);
        generatedTextures.Add(packed);
        return packed;
    }
}
