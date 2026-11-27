#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class ReleaseEnvironmentMaterialPostprocessor : AssetPostprocessor
{
    private void OnPostprocessModel(GameObject gameObject)
    {
        if (IsReleaseAsset()) Apply(gameObject, assetPath);
    }

    public static void Apply(GameObject gameObject, string assetPath)
    {
        const string folder = "Assets/Materials/Release";
        Directory.CreateDirectory(folder);
        Dictionary<Texture2D, Texture2D> roughnessCache = new Dictionary<Texture2D, Texture2D>();
        bool changed = false;
        foreach (MeshRenderer renderer in gameObject.GetComponentsInChildren<MeshRenderer>(true))
        {
            Material[] sourceMaterials = renderer.sharedMaterials;
            Material[] materials = new Material[sourceMaterials.Length];
            for (int i = 0; i < sourceMaterials.Length; i++)
            {
                Material source = sourceMaterials[i];
                if (source == null) continue;
                string sourceName = string.IsNullOrWhiteSpace(source.name) ? "ReleaseMaterial" : source.name;
                string materialName = Sanitize(Path.GetFileNameWithoutExtension(assetPath) + "_" + sourceName);
                string path = $"{folder}/{materialName}.mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                bool created = material == null;
                if (created)
                {
                    Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                    if (shader == null) continue;
                    material = new Material(shader) { name = materialName };
                }
                ConvertMaterial(material, source, materialName, roughnessCache);
                if (created) AssetDatabase.CreateAsset(material, path);
                else EditorUtility.SetDirty(material);
                materials[i] = material;
                changed = true;
            }
            renderer.sharedMaterials = materials;
        }
        if (changed) AssetDatabase.SaveAssets();
    }

    private bool IsReleaseAsset() => assetPath.Replace('\\', '/').Contains("/ReleaseKit/");

    private static void ConvertMaterial(Material target, Material source, string materialName, Dictionary<Texture2D, Texture2D> roughnessCache)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader != null) target.shader = shader;
        Color baseColor = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.HasProperty("_Color") ? source.GetColor("_Color") : source.color;
        target.SetColor("_BaseColor", baseColor);
        Texture baseMap = FirstTexture(source, "_BaseMap", "_MainTex");
        Texture normalMap = FirstTexture(source, "_BumpMap", "_NORMALMAP");
        Texture metallicGloss = FirstTexture(source, "_MetallicGlossMap");
        Texture specGloss = FirstTexture(source, "_SpecGlossMap");
        Texture roughness = FirstTexture(source, "_RoughnessMap");
        Texture occlusion = FirstTexture(source, "_OcclusionMap");
        Texture emissionMap = FirstTexture(source, "_EmissionMap");
        float metallic = source.HasProperty("_Metallic") ? source.GetFloat("_Metallic") : 0f;
        float smoothness = source.HasProperty("_Smoothness") ? source.GetFloat("_Smoothness") : source.HasProperty("_Glossiness") ? source.GetFloat("_Glossiness") : 0.25f;
        float scalarRoughness = source.HasProperty("_Roughness") ? source.GetFloat("_Roughness") : 1f - smoothness;

        target.SetTexture("_BaseMap", baseMap);
        SetKeyword(target, "_BASEMAP", baseMap != null);
        target.SetTexture("_BumpMap", normalMap);
        target.SetFloat("_BumpScale", source.HasProperty("_BumpScale") ? source.GetFloat("_BumpScale") : 1f);
        SetKeyword(target, "_NORMALMAP", normalMap != null);
        target.SetTexture("_MetallicGlossMap", metallicGloss);
        target.SetTexture("_SpecGlossMap", specGloss);
        if (metallicGloss == null && specGloss == null && roughness is Texture2D roughnessTexture)
        {
            if (!roughnessCache.TryGetValue(roughnessTexture, out Texture2D packed))
            {
                packed = CreatePackedRoughnessAsset(roughnessTexture, Sanitize(materialName + "_Roughness"));
                roughnessCache.Add(roughnessTexture, packed);
            }
            target.SetTexture("_MetallicGlossMap", packed);
            metallicGloss = packed;
            smoothness = 1f;
        }
        target.SetFloat("_Metallic", Mathf.Clamp01(metallic));
        target.SetFloat("_Smoothness", Mathf.Clamp01(metallicGloss != null || specGloss != null ? smoothness : 1f - scalarRoughness));
        SetKeyword(target, "_METALLICSPECGLOSSMAP", metallicGloss != null);
        SetKeyword(target, "_SPECGLOSSMAP", specGloss != null);
        target.SetTexture("_OcclusionMap", occlusion);
        target.SetFloat("_OcclusionStrength", source.HasProperty("_OcclusionStrength") ? source.GetFloat("_OcclusionStrength") : 1f);
        SetKeyword(target, "_OCCLUSIONMAP", occlusion != null);
        target.SetTexture("_EmissionMap", emissionMap);
        Color emission = source.HasProperty("_EmissionColor") ? source.GetColor("_EmissionColor") : Color.black;
        target.SetColor("_EmissionColor", emission);
        bool emissive = emission.maxColorComponent > 0.001f || emissionMap != null;
        SetKeyword(target, "_EMISSION", emissive);
        target.globalIlluminationFlags = emissive ? MaterialGlobalIlluminationFlags.RealtimeEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;

        string lowerName = materialName.ToLowerInvariant();
        bool transparent = lowerName.Contains("glass") || lowerName.Contains("window") || baseColor.a < 0.999f;
        if (transparent)
        {
            float alpha = baseColor.a < 0.999f ? baseColor.a : 0.55f;
            RaceMaterialFactory.MakeTransparent(target, new Color(baseColor.r, baseColor.g, baseColor.b, alpha));
        }
        if (source.HasProperty("_Cull") && target.HasProperty("_Cull") && !transparent) target.SetFloat("_Cull", source.GetFloat("_Cull"));
        CopyTextureTransform(target, source, "_BaseMap", "_MainTex");
        CopyTextureTransform(target, source, "_BumpMap", "_NORMALMAP");
        CopyTextureTransform(target, source, "_MetallicGlossMap");
        if (roughness != null && source.HasProperty("_RoughnessMap") && target.HasProperty("_MetallicGlossMap"))
        {
            target.SetTextureScale("_MetallicGlossMap", source.GetTextureScale("_RoughnessMap"));
            target.SetTextureOffset("_MetallicGlossMap", source.GetTextureOffset("_RoughnessMap"));
        }
        CopyTextureTransform(target, source, "_SpecGlossMap");
        CopyTextureTransform(target, source, "_OcclusionMap");
        CopyTextureTransform(target, source, "_EmissionMap");
        target.enableInstancing = true;
    }

    private static Texture FirstTexture(Material material, params string[] properties)
    {
        foreach (string property in properties)
            if (material.HasProperty(property) && material.GetTexture(property) != null) return material.GetTexture(property);
        return null;
    }

    private static void CopyTextureTransform(Material target, Material source, params string[] properties)
    {
        string property = properties[0];
        foreach (string candidate in properties)
        {
            if (!source.HasProperty(candidate) || source.GetTexture(candidate) == null) continue;
            property = candidate;
            break;
        }
        if (!target.HasProperty(property) || !source.HasProperty(property)) return;
        target.SetTextureScale(property, source.GetTextureScale(property));
        target.SetTextureOffset(property, source.GetTextureOffset(property));
    }

    private static Texture2D CreatePackedRoughnessAsset(Texture2D source, string assetName)
    {
        const string folder = "Assets/Materials/Release";
        string path = $"{folder}/{assetName}.asset";
        Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;
        RenderTexture temporary = new RenderTexture(source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        Graphics.Blit(source, temporary);
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = temporary;
        Texture2D packed = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true)
        {
            name = assetName,
            wrapMode = TextureWrapMode.Repeat,
            filterMode = FilterMode.Trilinear
        };
        packed.ReadPixels(new Rect(0f, 0f, source.width, source.height), 0, 0);
        Color[] pixels = packed.GetPixels();
        for (int i = 0; i < pixels.Length; i++)
        {
            float roughness = Mathf.Clamp01(pixels[i].r);
            pixels[i] = new Color(0f, roughness, roughness, 1f - roughness);
        }
        packed.SetPixels(pixels);
        packed.Apply(false, true);
        RenderTexture.active = previous;
        Object.DestroyImmediate(temporary);
        AssetDatabase.CreateAsset(packed, path);
        return packed;
    }

    private static void SetKeyword(Material material, string keyword, bool enabled)
    {
        if (enabled) material.EnableKeyword(keyword);
        else material.DisableKeyword(keyword);
    }

    private static string Sanitize(string value)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
        return string.IsNullOrWhiteSpace(value) ? "ReleaseMaterial" : value;
    }
}
#endif
