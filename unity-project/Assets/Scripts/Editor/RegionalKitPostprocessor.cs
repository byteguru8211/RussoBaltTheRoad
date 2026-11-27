using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public sealed class RegionalKitPostprocessor : AssetPostprocessor
{
    private const string MaterialFolder = "Assets/Materials/Regional";

    private void OnPreprocessModel(GameObject root)
    {
        if (!assetPath.StartsWith("Assets/Resources/Environment/RegionalKit/")) return;
        ModelImporter importer = assetImporter as ModelImporter;
        if (importer == null) return;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.animationType = ModelImporterAnimationType.None;
        importer.addCollider = false;
        importer.isReadable = false;
        importer.meshCompression = ModelImporterMeshCompression.Medium;
    }

    public static void Apply(GameObject root)
    {
        if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder("Assets/Materials", "Regional");
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) return;
        var cache = new Dictionary<string, Material>();
        bool changed = false;
        foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            Material[] source = renderer.sharedMaterials;
            Material[] materials = new Material[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                Material sourceMaterial = source[i];
                if (sourceMaterial == null) continue;
                string materialName = sourceMaterial.name.Replace('/', '_');
                if (!cache.TryGetValue(materialName, out Material material))
                {
                    string path = $"{MaterialFolder}/{materialName}.mat";
                    material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (material == null)
                    {
                        material = new Material(shader);
                        AssetDatabase.CreateAsset(material, path);
                    }
                    cache.Add(materialName, material);
                }
                Color color = sourceMaterial.HasProperty("_BaseColor") ? sourceMaterial.GetColor("_BaseColor") : sourceMaterial.color;
                float metallic = sourceMaterial.HasProperty("_Metallic") ? sourceMaterial.GetFloat("_Metallic") : 0f;
                float smoothness = sourceMaterial.HasProperty("_Smoothness") ? sourceMaterial.GetFloat("_Smoothness")
                    : sourceMaterial.HasProperty("_Roughness") ? 1f - sourceMaterial.GetFloat("_Roughness") : 0.65f;
                material.SetColor("_BaseColor", color);
                material.SetFloat("_Metallic", metallic);
                material.SetFloat("_Smoothness", Mathf.Clamp01(smoothness));
                if (sourceMaterial.HasProperty("_EmissionColor") && sourceMaterial.GetColor("_EmissionColor").maxColorComponent > 0.001f)
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", sourceMaterial.GetColor("_EmissionColor"));
                }
                EditorUtility.SetDirty(material);
                materials[i] = material;
                changed = true;
            }
            renderer.sharedMaterials = materials;
        }
        if (changed) AssetDatabase.SaveAssets();
    }
}
