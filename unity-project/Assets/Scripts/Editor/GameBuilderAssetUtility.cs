using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

public static class GameBuilderAssetUtility
{
    private const string GlassShaderPath = "Assets/Shaders/Generated/RussoBaltGlass.shader";
    private const string RaceVolumePath = "Assets/Settings/RaceVolumeProfile.asset";

    public static void ConfigureModelImporter(string assetPath, bool character)
    {
        ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
        if (importer == null) return;
        bool changed = false;
        changed |= SetIfChanged(importer.useFileScale, true, value => importer.useFileScale = value);
        changed |= SetIfChanged(importer.globalScale, 1f, value => importer.globalScale = value);
        changed |= SetIfChanged(importer.importNormals, ModelImporterNormals.Import, value => importer.importNormals = value);
        changed |= SetIfChanged(importer.importTangents, ModelImporterTangents.CalculateMikk, value => importer.importTangents = value);
        changed |= SetIfChanged(importer.materialImportMode, ModelImporterMaterialImportMode.ImportStandard, value => importer.materialImportMode = value);
        ModelImporterAnimationType animation = character ? ModelImporterAnimationType.Generic : ModelImporterAnimationType.None;
        changed |= SetIfChanged(importer.animationType, animation, value => importer.animationType = value);
        if (changed) importer.SaveAndReimport();
    }

    public static void ConfigureTextureImporters()
    {
        foreach (string folder in new[] { "Assets/GameAssets", "Assets/Resources/Textures", "Assets/Resources/PreRace" })
        {
            if (!AssetDatabase.IsValidFolder(folder)) continue;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
                ConfigureTextureImporter(AssetDatabase.GUIDToAssetPath(guid));
        }
    }

    private static void ConfigureTextureImporter(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null) return;
        string name = Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
        bool normal = name.Contains("normal") || name.Contains("_nrm") || name.EndsWith("_n");
        bool data = normal || name.Contains("roughness") || name.Contains("metallic") || name.Contains("occlusion") || name.Contains("height") || name.Contains("transmission") || name.Contains("opacity");
        bool particle = name.Contains("particle") || name.Contains("smoke") || name.Contains("dust");
        bool changed = false;
        TextureImporterType desiredType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        changed |= SetIfChanged(importer.textureType, desiredType, value => importer.textureType = value);
        changed |= SetIfChanged(importer.sRGBTexture, !data, value => importer.sRGBTexture = value);
        changed |= SetIfChanged(importer.mipmapEnabled, true, value => importer.mipmapEnabled = value);
        changed |= SetIfChanged(importer.wrapMode, particle ? TextureWrapMode.Clamp : TextureWrapMode.Repeat, value => importer.wrapMode = value);
        changed |= SetIfChanged(importer.filterMode, FilterMode.Trilinear, value => importer.filterMode = value);
        changed |= SetIfChanged(importer.textureCompression, normal ? TextureImporterCompression.Compressed : TextureImporterCompression.CompressedHQ, value => importer.textureCompression = value);
        changed |= SetIfChanged(importer.alphaIsTransparency, name.Contains("glass") || name.Contains("window") || particle, value => importer.alphaIsTransparency = value);
        if (changed) importer.SaveAndReimport();
    }

    public static void ConvertMaterials(GameObject instance, string prefix)
    {
        foreach (MeshRenderer renderer in instance.GetComponentsInChildren<MeshRenderer>())
        {
            Material[] converted = new Material[renderer.sharedMaterials.Length];
            for (int i = 0; i < renderer.sharedMaterials.Length; i++)
            {
                Material source = renderer.sharedMaterials[i];
                if (source == null) continue;
                string safe = new string(source.name.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
                if (safe.Length == 0) safe = "Material";
                string materialPath = $"Assets/Materials/{prefix}_{safe}.asset";
                string sourcePath = AssetDatabase.GetAssetPath(source);
                bool alreadyGenerated = sourcePath.StartsWith($"Assets/Materials/{prefix}_", System.StringComparison.Ordinal) && sourcePath.EndsWith(".asset", System.StringComparison.Ordinal);
                if (alreadyGenerated) materialPath = sourcePath;
                Material material = alreadyGenerated ? source : AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                bool createAsset = material == null;
                if (createAsset)
                {
                    material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = prefix + "_" + safe };
                }

                Color baseColor = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
                Texture baseMap = FirstTexture(source, "_BaseMap", "_MainTex");
                Texture normalMap = FirstTexture(source, "_BumpMap", "_NORMALMAP");
                if (normalMap is Texture2D normalTexture) normalMap = EnsureNormalTextureAsset(normalTexture, prefix, safe);
                Texture metallicGloss = FirstTexture(source, "_MetallicGlossMap");
                Texture specGloss = FirstTexture(source, "_SpecGlossMap");
                Texture roughness = FirstTexture(source, "_RoughnessMap");
                Texture occlusion = FirstTexture(source, "_OcclusionMap");
                Texture emissionMap = FirstTexture(source, "_EmissionMap");
                Texture transmissionMap = FirstTexture(source, "_TransmissionMap", "_TransmissionMask");
                Texture thicknessMap = FirstTexture(source, "_ThicknessMap");
                Color emission = source.HasProperty("_EmissionColor") ? source.GetColor("_EmissionColor") : Color.black;
                float sourceTransmission = source.HasProperty("_Transmission") ? source.GetFloat("_Transmission") : 0f;
                float thickness = source.HasProperty("_Thickness") ? source.GetFloat("_Thickness") : 0.5f;
                float sourceAlpha = source.HasProperty("_Alpha") ? source.GetFloat("_Alpha") : baseColor.a;
                string sourceName = source.name.ToLowerInvariant();
                bool glass = sourceName.Contains("glass") || sourceName.Contains("window") || sourceName.Contains("windscreen") || sourceName.Contains("windshield") || sourceName.Contains("lens") || baseColor.a < 0.999f || sourceTransmission > 0.001f || transmissionMap != null;

                if (glass)
                {
                    material.shader = EnsureGlassShader();
                    material.SetColor("_BaseColor", baseColor);
                    material.SetTexture("_BaseMap", baseMap);
                    material.SetTexture("_BumpMap", normalMap);
                    material.SetTexture("_TransmissionMap", transmissionMap);
                    material.SetTexture("_ThicknessMap", thicknessMap);
                    if (normalMap != null)
                    {
                        material.SetFloat("_BumpScale", source.HasProperty("_BumpScale") ? source.GetFloat("_BumpScale") : 1f);
                    }
                    material.SetFloat("_Metallic", source.HasProperty("_Metallic") ? source.GetFloat("_Metallic") : 0f);
                    material.SetFloat("_Roughness", source.HasProperty("_Roughness") ? source.GetFloat("_Roughness") : source.HasProperty("_Glossiness") ? 1f - source.GetFloat("_Glossiness") : 0.12f);
                    material.SetFloat("_Transmission", sourceTransmission > 0.001f ? sourceTransmission : 0.68f);
                    material.SetFloat("_Thickness", Mathf.Max(0.02f, thickness));
                    material.SetTexture("_EmissionMap", emissionMap);
                    material.SetColor("_EmissionColor", emission);
                    float alpha = sourceAlpha < 0.999f ? sourceAlpha : 0.3f;
                    Color shaderColor = baseColor;
                    shaderColor.a = 1f;
                    material.SetColor("_BaseColor", shaderColor);
                    material.SetFloat("_Alpha", alpha);
                    material.renderQueue = 3000;
                    material.SetOverrideTag("RenderType", "Transparent");
                    material.globalIlluminationFlags = emission.maxColorComponent > 0.001f || emissionMap != null ? MaterialGlobalIlluminationFlags.RealtimeEmissive : MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                    if (material.shader == null || material.shader.name == "Universal Render Pipeline/Lit")
                    {
                        RaceMaterialFactory.MakeTransparent(material, new Color(baseColor.r, baseColor.g, baseColor.b, alpha));
                        if (normalMap != null) material.EnableKeyword("_NORMALMAP");
                    }
                }
                else
                {
                    material.shader = Shader.Find("Universal Render Pipeline/Lit");
                    material.SetColor("_BaseColor", baseColor);
                    if (baseMap != null) material.SetTexture("_BaseMap", baseMap);
                    if (normalMap != null)
                    {
                        material.SetTexture("_BumpMap", normalMap);
                        material.SetFloat("_BumpScale", source.HasProperty("_BumpScale") ? source.GetFloat("_BumpScale") : 1f);
                        material.EnableKeyword("_NORMALMAP");
                    }
                    else material.DisableKeyword("_NORMALMAP");
                    material.SetTexture("_BaseMap", baseMap);
                    material.SetTexture("_BumpMap", normalMap);
                    material.SetTexture("_MetallicGlossMap", null);
                    material.SetTexture("_SpecGlossMap", null);
                    material.SetTexture("_OcclusionMap", occlusion);

                    if (metallicGloss != null)
                    {
                        material.SetTexture("_MetallicGlossMap", metallicGloss);
                        material.SetFloat("_WorkflowMode", 1f);
                        material.SetFloat("_Metallic", source.HasProperty("_Metallic") ? source.GetFloat("_Metallic") : 0f);
                        material.SetFloat("_Smoothness", source.HasProperty("_Smoothness") ? source.GetFloat("_Smoothness") : source.HasProperty("_Glossiness") ? source.GetFloat("_Glossiness") : 0.3f);
                        material.EnableKeyword("_METALLICSPECGLOSSMAP");
                        material.DisableKeyword("_SPECGLOSSMAP");
                    }
                    else if (specGloss != null)
                    {
                        material.SetTexture("_SpecGlossMap", specGloss);
                        material.SetFloat("_WorkflowMode", 0f);
                        material.SetColor("_SpecColor", source.HasProperty("_SpecColor") ? source.GetColor("_SpecColor") : new Color(0.2f, 0.2f, 0.2f));
                        material.SetFloat("_GlossMapScale", source.HasProperty("_GlossMapScale") ? source.GetFloat("_GlossMapScale") : 1f);
                        material.EnableKeyword("_SPECGLOSSMAP");
                        material.DisableKeyword("_METALLICSPECGLOSSMAP");
                    }
                    else if (roughness != null)
                    {
                        material.SetTexture("_MetallicGlossMap", CreatePackedRoughnessAsset(roughness as Texture2D, prefix, safe));
                        material.SetFloat("_WorkflowMode", 1f);
                        material.SetFloat("_Metallic", source.HasProperty("_Metallic") ? source.GetFloat("_Metallic") : 0f);
                        material.SetFloat("_Smoothness", 1f);
                        material.EnableKeyword("_METALLICSPECGLOSSMAP");
                        material.DisableKeyword("_SPECGLOSSMAP");
                    }
                    else
                    {
                        material.SetFloat("_Metallic", source.HasProperty("_Metallic") ? source.GetFloat("_Metallic") : 0.2f);
                        material.SetFloat("_Smoothness", source.HasProperty("_Smoothness") ? source.GetFloat("_Smoothness") : source.HasProperty("_Glossiness") ? source.GetFloat("_Glossiness") : 0.3f);
                        material.DisableKeyword("_METALLICSPECGLOSSMAP");
                        material.DisableKeyword("_SPECGLOSSMAP");
                    }
                    if (occlusion != null)
                    {
                        material.SetTexture("_OcclusionMap", occlusion);
                        material.SetFloat("_OcclusionStrength", source.HasProperty("_OcclusionStrength") ? source.GetFloat("_OcclusionStrength") : 1f);
                        material.EnableKeyword("_OCCLUSIONMAP");
                    }
                    else material.DisableKeyword("_OCCLUSIONMAP");
                    material.SetTexture("_EmissionMap", emissionMap);
                    material.SetColor("_EmissionColor", emission);
                    if (emission.maxColorComponent > 0.001f || emissionMap != null)
                    {
                        material.EnableKeyword("_EMISSION");
                        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                    }
                    else
                    {
                        material.DisableKeyword("_EMISSION");
                        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                    }
                }
                CopyTextureTransform(material, source, "_BaseMap", source.HasProperty("_BaseMap") ? "_BaseMap" : "_MainTex");
                CopyTextureTransform(material, source, "_BumpMap");
                CopyTextureTransform(material, source, "_EmissionMap");
                CopyTextureTransform(material, source, "_MetallicGlossMap");
                CopyTextureTransform(material, source, "_SpecGlossMap");
                CopyTextureTransform(material, source, "_OcclusionMap");
                CopyTextureTransform(material, source, "_TransmissionMap");
                CopyTextureTransform(material, source, "_ThicknessMap");
                if (createAsset) AssetDatabase.CreateAsset(material, materialPath);
                else EditorUtility.SetDirty(material);
                converted[i] = material;
            }
            renderer.sharedMaterials = converted;
        }
    }

    private static Texture FirstTexture(Material material, params string[] properties)
    {
        foreach (string property in properties)
        {
            if (material.HasProperty(property))
            {
                Texture texture = material.GetTexture(property);
                if (texture != null) return texture;
            }
        }
        return null;
    }

    private static void CopyTextureTransform(Material target, Material source, string property)
    {
        if (!target.HasProperty(property) || !source.HasProperty(property)) return;
        target.SetTextureScale(property, source.GetTextureScale(property));
        target.SetTextureOffset(property, source.GetTextureOffset(property));
    }

    private static void CopyTextureTransform(Material target, Material source, string targetProperty, string sourceProperty)
    {
        if (!target.HasProperty(targetProperty) || !source.HasProperty(sourceProperty)) return;
        target.SetTextureScale(targetProperty, source.GetTextureScale(sourceProperty));
        target.SetTextureOffset(targetProperty, source.GetTextureOffset(sourceProperty));
    }

    private static Color[] ReadTexturePixels(Texture2D source)
    {
        Texture2D raw = new Texture2D(source.width, source.height, source.format, source.mipmapCount > 1, true);
        Graphics.CopyTexture(source, raw);
        Color[] pixels = raw.GetPixels();
        UnityEngine.Object.DestroyImmediate(raw);
        return pixels;
    }

    private static Texture2D EnsureNormalTextureAsset(Texture2D source, string prefix, string materialName)
    {
        string sourcePath = AssetDatabase.GetAssetPath(source);
        TextureImporter importer = AssetImporter.GetAtPath(sourcePath) as TextureImporter;
        if (importer != null && importer.textureType == TextureImporterType.NormalMap && !importer.sRGBTexture) return source;
        string safeSource = new string(source.name.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
        string path = $"Assets/Materials/Generated/{prefix}_{materialName}_{safeSource}_Normal.png";
        Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;
        Texture2D readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
        Color[] pixels = ReadTexturePixels(source);
        readable.SetPixels(pixels);
        readable.Apply(false, false);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, readable.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(readable);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        TextureImporter generatedImporter = AssetImporter.GetAtPath(path) as TextureImporter;
        if (generatedImporter != null)
        {
            generatedImporter.textureType = TextureImporterType.NormalMap;
            generatedImporter.sRGBTexture = false;
            generatedImporter.wrapMode = TextureWrapMode.Repeat;
            generatedImporter.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Texture2D CreatePackedRoughnessAsset(Texture2D source, string prefix, string materialName)
    {
        if (source == null) return null;
        string safeSource = new string(source.name.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());
        string path = $"Assets/Materials/Generated/{prefix}_{materialName}_{safeSource}_Roughness.png";
        Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (existing != null) return existing;
        Color[] pixels = ReadTexturePixels(source);
        Texture2D readable = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, true);
        for (int i = 0; i < pixels.Length; i++)
        {
            float roughness = Mathf.Clamp01(pixels[i].r);
            pixels[i] = new Color(0f, roughness, roughness, 1f - roughness);
        }
        readable.SetPixels(pixels);
        readable.Apply(false, false);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllBytes(path, readable.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(readable);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        ConfigureTextureImporter(path);
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    private static Shader EnsureGlassShader()
    {
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(GlassShaderPath);
        if (shader != null) return shader;
        Directory.CreateDirectory(Path.GetDirectoryName(GlassShaderPath));
        shader = ShaderUtil.CreateShaderAsset(GlassShaderSource);
        if (shader == null) return Shader.Find("Universal Render Pipeline/Lit");
        AssetDatabase.CreateAsset(shader, GlassShaderPath);
        return shader;
    }

    public static void AddLODGroup(GameObject wrapper, GameObject[] models, float[] transitionHeights)
    {
        LODGroup group = wrapper.GetComponent<LODGroup>();
        if (group == null) group = wrapper.AddComponent<LODGroup>();
        LOD[] lods = new LOD[models.Length];
        for (int i = 0; i < models.Length; i++)
        {
            Renderer[] renderers = models[i].GetComponentsInChildren<MeshRenderer>();
            lods[i] = new LOD(transitionHeights[i], renderers);
        }
        group.SetLODs(lods);
    }

    public static Transform CreateSocket(GameObject parent, string name, Vector3 localPosition)
    {
        Transform existing = parent.transform.Find(name);
        if (existing != null) return existing;
        GameObject socket = new GameObject(name);
        socket.transform.SetParent(parent.transform, false);
        socket.transform.localPosition = localPosition;
        return socket.transform;
    }

    public static VolumeProfile EnsureRaceVolumeProfile()
    {
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(RaceVolumePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "RaceVolumeProfile";
            AssetDatabase.CreateAsset(profile, RaceVolumePath);
        }
        if (!profile.TryGet(out Bloom bloom)) bloom = profile.Add<Bloom>(true);
        bloom.intensity.overrideState = bloom.threshold.overrideState = bloom.scatter.overrideState = true;
        bloom.intensity.value = 0.28f;
        bloom.threshold.value = 1.05f;
        bloom.scatter.value = 0.55f;
        if (!profile.TryGet(out ColorAdjustments color)) color = profile.Add<ColorAdjustments>(true);
        color.postExposure.overrideState = color.contrast.overrideState = color.saturation.overrideState = true;
        color.postExposure.value = 0.15f;
        color.contrast.value = 12f;
        color.saturation.value = 6f;
        if (!profile.TryGet(out Vignette vignette)) vignette = profile.Add<Vignette>(true);
        vignette.intensity.overrideState = vignette.smoothness.overrideState = true;
        vignette.intensity.value = 0.18f;
        vignette.smoothness.value = 0.35f;
        EditorUtility.SetDirty(profile);
        return profile;
    }

    public static void ConfigureSceneRendering(Scene scene, bool race)
    {
        GameObject cameraObject = scene.GetRootGameObjects().FirstOrDefault(o => o.CompareTag("MainCamera"));
        if (cameraObject == null)
        {
            cameraObject = new GameObject("Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 3f, -8f);
        }
        Camera camera = EnsureComponent<Camera>(cameraObject);
        EnsureComponent<AudioListener>(cameraObject);
        UniversalAdditionalCameraData cameraData = EnsureComponent<UniversalAdditionalCameraData>(cameraObject);
        cameraData.renderPostProcessing = race;
        cameraData.renderShadows = true;
        cameraData.volumeLayerMask = ~0;
        cameraData.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
        cameraData.antialiasingQuality = AntialiasingQuality.Medium;
        camera.allowHDR = true;
        camera.allowMSAA = false;
        camera.farClipPlane = Mathf.Max(camera.farClipPlane, race ? 1800f : 1000f);
        if (race) camera.clearFlags = CameraClearFlags.Skybox;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Camera sceneCamera in root.GetComponentsInChildren<Camera>(true))
            {
                if (sceneCamera == camera) continue;
                UniversalAdditionalCameraData additional = EnsureComponent<UniversalAdditionalCameraData>(sceneCamera.gameObject);
                additional.renderPostProcessing = race;
                additional.renderShadows = true;
                additional.volumeLayerMask = ~0;
                additional.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
                additional.antialiasingQuality = AntialiasingQuality.Medium;
                sceneCamera.allowHDR = true;
                sceneCamera.allowMSAA = false;
                sceneCamera.farClipPlane = Mathf.Max(sceneCamera.farClipPlane, race ? 1800f : 1000f);
                if (race) sceneCamera.clearFlags = CameraClearFlags.Skybox;
            }
        }

        Light sun = null;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (Light light in root.GetComponentsInChildren<Light>(true))
            {
                if (light.type != LightType.Directional) continue;
                light.shadows = LightShadows.Soft;
                light.shadowStrength = 0.82f;
                light.shadowBias = 0.035f;
                light.shadowNormalBias = 0.35f;
                UniversalAdditionalLightData lightData = EnsureComponent<UniversalAdditionalLightData>(light.gameObject);
                lightData.softShadowQuality = SoftShadowQuality.High;
                if (light.gameObject.activeInHierarchy && (sun == null || light.name == "Directional Light")) sun = light;
            }
        }
        if (sun == null)
        {
            GameObject sunObject = new GameObject("Directional Light");
            sunObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
            sun = sunObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.intensity = 1.2f;
        }
        RenderSettings.sun = sun;
        if (race)
        {
            VolumeProfile profile = EnsureRaceVolumeProfile();
            GameObject volumeObject = scene.GetRootGameObjects().FirstOrDefault(o => o.name == "AtmosphereVolume");
            if (volumeObject == null) volumeObject = new GameObject("AtmosphereVolume");
            Volume volume = EnsureComponent<Volume>(volumeObject);
            volume.sharedProfile = profile;
            volume.isGlobal = true;
            volume.priority = 5f;
        }
        ConfigurePipelineAsset();
    }

    private static void ConfigurePipelineAsset()
    {
        if (!(GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset pipeline)) return;
        pipeline.shadowDistance = 120f;
        pipeline.mainLightShadowmapResolution = 2048;
        SerializedObject serialized = new SerializedObject(pipeline);
        serialized.Update();
        SetSerialized(serialized, "m_MainLightShadowsSupported", 1);
        SetSerialized(serialized, "m_SoftShadowsSupported", 1);
        SetSerialized(serialized, "m_RequireOpaqueTexture", 1);
        SetSerialized(serialized, "m_SupportsHDR", 1);
        SetSerialized(serialized, "m_SoftShadowQuality", 3);
        if (serialized.ApplyModifiedPropertiesWithoutUndo()) EditorUtility.SetDirty(pipeline);
    }

    private static void SetSerialized(SerializedObject serialized, string property, object value)
    {
        SerializedProperty field = serialized.FindProperty(property);
        if (field == null) return;
        if (field.propertyType == SerializedPropertyType.Float) field.floatValue = Convert.ToSingle(value);
        else if (field.propertyType == SerializedPropertyType.Integer) field.intValue = Convert.ToInt32(value);
        else if (field.propertyType == SerializedPropertyType.Boolean) field.boolValue = Convert.ToBoolean(value);
        else if (field.propertyType == SerializedPropertyType.Enum) field.enumValueIndex = Convert.ToInt32(value);
    }

    private static T EnsureComponent<T>(GameObject gameObject) where T : Component
    {
        T component = gameObject.GetComponent<T>();
        return component != null ? component : gameObject.AddComponent<T>();
    }

    private static bool SetIfChanged<T>(T current, T desired, Action<T> setter)
    {
        if (EqualityComparer<T>.Default.Equals(current, desired)) return false;
        setter(desired);
        return true;
    }

    private const string GlassShaderSource = @"
Shader ""RussoBalt/Generated/TransmissiveGlass""
{
    Properties
    {
        [MainTexture] _BaseMap(""Base Map"", 2D) = ""white"" {}
        [MainColor] _BaseColor(""Base Color"", Color) = (1,1,1,1)
        _BumpMap(""Normal Map"", 2D) = ""bump"" {}
        _BumpScale(""Normal Scale"", Float) = 1
        _Metallic(""Metallic"", Range(0,1)) = 0
        _Roughness(""Roughness"", Range(0,1)) = 0.12
        _TransmissionMap(""Transmission Map"", 2D) = ""white"" {}
        _Transmission(""Transmission"", Range(0,1)) = 0.68
        _ThicknessMap(""Thickness Map"", 2D) = ""white"" {}
        _Thickness(""Thickness"", Range(0.01,3)) = 0.5
        _Alpha(""Alpha"", Range(0,1)) = 0.3
        _EmissionMap(""Emission Map"", 2D) = ""white"" {}
        [HDR] _EmissionColor(""Emission Color"", Color) = (0,0,0,0)
    }
    SubShader
    {
        Tags { ""RenderType""=""Transparent"" ""Queue""=""Transparent"" ""RenderPipeline""=""UniversalPipeline"" }
        Pass
        {
            Tags { ""LightMode""=""UniversalForward"" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl""
            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl""
            #include ""Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl""
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                half4 _EmissionColor;
                float _BumpScale;
                float _Metallic;
                float _Roughness;
                float _Transmission;
                float _Thickness;
                float _Alpha;
            CBUFFER_END
            TEXTURE2D(_BaseMap);
            TEXTURE2D(_BumpMap);
            TEXTURE2D(_TransmissionMap);
            TEXTURE2D(_ThicknessMap);
            TEXTURE2D(_EmissionMap);
            SAMPLER(sampler_BaseMap);
            SAMPLER(sampler_BumpMap);
            SAMPLER(sampler_TransmissionMap);
            SAMPLER(sampler_ThicknessMap);
            SAMPLER(sampler_EmissionMap);
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 tangentWS : TEXCOORD3;
                float3 bitangentWS : TEXCOORD4;
                float4 screenPosition : TEXCOORD5;
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = normals.normalWS;
                output.tangentWS = normals.tangentWS;
                output.bitangentWS = normals.bitangentWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.screenPosition = positions.positionNDC;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                half4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv) * _BaseColor;
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                float3x3 tangentToWorld = float3x3(input.tangentWS.xyz, input.bitangentWS.xyz, input.normalWS.xyz);
                half3 normalWS = normalize(TransformTangentToWorld(normalTS, tangentToWorld));
                half transmission = SAMPLE_TEXTURE2D(_TransmissionMap, sampler_TransmissionMap, input.uv).r * _Transmission;
                half thickness = SAMPLE_TEXTURE2D(_ThicknessMap, sampler_ThicknessMap, input.uv).r * _Thickness;
                float2 screenUV = input.screenPosition.xy;
                float2 offset = normalWS.xy * thickness * transmission * 0.035;
                float blur = 0.001 + _Roughness * 0.004;
                half3 refracted = SampleSceneColor(screenUV + offset);
                refracted += SampleSceneColor(screenUV + offset + float2(blur, 0));
                refracted += SampleSceneColor(screenUV + offset - float2(blur, 0));
                refracted += SampleSceneColor(screenUV + offset + float2(0, blur));
                refracted += SampleSceneColor(screenUV + offset - float2(0, blur));
                refracted *= 0.2;
                Light mainLight = GetMainLight();
                half3 viewDirectionWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                half3 halfDirection = normalize(viewDirectionWS + mainLight.direction);
                half diffuse = saturate(dot(normalWS, mainLight.direction));
                half specular = pow(saturate(dot(normalWS, halfDirection)), mix(96, 8, _Roughness));
                half3 lighting = SampleSH(normalWS) + mainLight.color * diffuse;
                half3 color = lerp(baseColor.rgb * lighting, refracted * baseColor.rgb, transmission * 0.82);
                color += specular * mainLight.color * mix(0.6, 0.08, _Metallic);
                color += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _EmissionColor.rgb;
                half alpha = saturate(baseColor.a * _Alpha * (1.0 - transmission * 0.55) + 0.04);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
";
}
