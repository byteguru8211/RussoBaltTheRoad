#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BootSceneInstaller
{
    [MenuItem("RussoBalt/Install Startup Cinematic")]
    public static void Install()
    {
        const string path = "Assets/Scenes/Boot.unity";
        if (!System.IO.File.Exists(path))
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("GameManager").AddComponent<GameManager>();
            new GameObject("LanSessionController").AddComponent<LanSessionController>();
            GameObject camera = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0f, 2.75f, 11.5f);
            camera.transform.LookAt(new Vector3(0f, 2.45f, 0f));
            new GameObject("StartupCinematic").AddComponent<StartupCinematicController>();
            EditorSceneManager.SaveScene(scene, path);
        }
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        scenes.RemoveAll(item => item.path == path);
        scenes.Insert(0, new EditorBuildSettingsScene(path, true));
        const string runtime = "Assets/Scenes/Race_Runtime.unity";
        if (!scenes.Exists(item => item.path == runtime)) scenes.Add(new EditorBuildSettingsScene(runtime, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("Startup cinematic installed as scene 0");
    }
}
#endif
