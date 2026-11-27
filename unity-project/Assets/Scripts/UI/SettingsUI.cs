using UnityEngine;
using UnityEngine.UI;

public class SettingsUI : MonoBehaviour
{
    private Canvas canvas;
    private Button[] cameraButtons;

    public static void Show(Transform parent = null)
    {
        GameObject go = new GameObject("SettingsUI", typeof(SettingsUI));
        go.GetComponent<SettingsUI>().Build(parent);
    }

    private void Build(Transform parent)
    {
        canvas = parent != null ? parent.GetComponent<Canvas>() : UIUtils.CreateCanvas("SettingsCanvas", 70);
        if (parent == null)
            UIUtils.Panel(canvas.transform, "Shade", new Color(0.01f, 0.02f, 0.015f, 0.82f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        RectTransform panel = UIUtils.Panel(canvas.transform, "SettingsPanel", UITheme.Dark, new Vector2(0.22f, 0.05f), new Vector2(0.78f, 0.95f), Vector2.zero, Vector2.zero).rectTransform;
        UIUtils.Text(panel, "Title", "Настройки", 48, UITheme.Cream, TextAnchor.MiddleCenter, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -80), new Vector2(0, -20));
        SettingsData settings = GameManager.Instance.Settings;
        UIUtils.Text(panel, "NameLabel", "Имя гонщика", 22, UITheme.Cream, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -128), new Vector2(250, -100));
        UIUtils.InputField(panel, "PlayerName", GameManager.Instance.Profile.playerName, new Vector2(300, -136), v => GameManager.Instance.Profile.playerName = v);
        UIUtils.Slider(panel, "Master", "Общая громкость", settings.masterVolume, new Vector2(40, -185), v => { settings.masterVolume = v; AudioListener.volume = v; });
        UIUtils.Slider(panel, "Engine", "Двигатель", settings.engineVolume, new Vector2(40, -250), v => settings.engineVolume = v);
        UIUtils.Slider(panel, "Ambient", "Мир", settings.ambientVolume, new Vector2(40, -315), v => settings.ambientVolume = v);
        UIUtils.Slider(panel, "Music", "Музыка меню", settings.musicVolume, new Vector2(40, -380), v => settings.musicVolume = v);
        UIUtils.Slider(panel, "Steering", "Чувствительность руля", settings.steeringSensitivity, new Vector2(40, -445), v => settings.steeringSensitivity = v, 0.5f, 1.5f);
        UIUtils.Text(panel, "CameraLabel", "Камера", 22, UITheme.Cream, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -505), new Vector2(250, -477));
        string[] cameraNames = { "Погоня", "Капот", "Кокпит", "Кино", "Задняя" };
        cameraButtons = new Button[cameraNames.Length];
        const float cameraButtonWidth = 0.168f;
        const float cameraButtonStep = 0.183f;
        for (int i = 0; i < cameraNames.Length; i++)
        {
            int mode = i;
            float left = 0.06f + i * cameraButtonStep;
            cameraButtons[i] = UIUtils.Button(panel, "Camera_" + i, cameraNames[i], new Vector2(left, 0.40f), new Vector2(left + cameraButtonWidth, 0.455f), Vector2.zero, Vector2.zero, () =>
            {
                settings.cameraMode = (CameraMode)mode;
                GameManager.Instance.SelectCameraMode(mode);
                RefreshCameraButtons();
            }, 18);
        }
        RefreshCameraButtons();
        UIUtils.Toggle(panel, "Shake", "Тряска камеры", settings.cameraShake, new Vector2(40, -615), v => settings.cameraShake = v);
        UIUtils.Toggle(panel, "Clutch", "Помощь со сцеплением", settings.automaticClutch, new Vector2(40, -665), v => settings.automaticClutch = v);
        UIUtils.Toggle(panel, "Subtitles", "Субтитры", settings.subtitles, new Vector2(40, -715), v => settings.subtitles = v);
        UIUtils.Toggle(panel, "AutomaticTransmission", "Автоматическая коробка", settings.automaticTransmission, new Vector2(40, -765), v => settings.automaticTransmission = v);
        UIUtils.Button(panel, "Save", "Сохранить", new Vector2(0.25f, 0.02f), new Vector2(0.75f, 0.10f), Vector2.zero, Vector2.zero, () =>
        {
            GameManager.Instance.Profile.playerName = SaveSystem.NormalizePlayerName(GameManager.Instance.Profile.playerName);
            GameManager.Instance.Save();
            RefreshCameraButtons();
            if (parent == null)
            {
                Destroy(canvas.gameObject);
                Destroy(gameObject);
            }
            else
                Destroy(panel.gameObject);
        }, 26);
    }

    private void RefreshCameraButtons()
    {
        int activeMode = (int)GameManager.Instance.Settings.cameraMode;
        for (int i = 0; i < cameraButtons.Length; i++)
        {
            bool active = i == activeMode;
            UIUtils.SetButtonColor(cameraButtons[i], active ? UITheme.Brass : UITheme.Panel);
        }
    }
}
