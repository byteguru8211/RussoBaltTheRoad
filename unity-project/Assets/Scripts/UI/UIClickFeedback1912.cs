using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DefaultExecutionOrder(400)]
public class UIClickFeedback1912 : MonoBehaviour
{
    private static UIClickFeedback1912 instance;
    private readonly HashSet<Button> registeredButtons = new HashSet<Button>();
    private readonly List<Button> staleButtons = new List<Button>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (instance != null)
            return;
        GameObject feedbackObject = new GameObject("UIClickFeedback1912");
        feedbackObject.AddComponent<UIClickFeedback1912>();
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Update()
    {
        foreach (Button button in FindObjectsByType<Button>())
        {
            if (button == null || registeredButtons.Contains(button))
                continue;
            registeredButtons.Add(button);
            button.onClick.AddListener(PlayClick);
        }

        if (registeredButtons.Count == 0)
            return;
        staleButtons.Clear();
        foreach (Button button in registeredButtons)
        {
            if (button == null)
                staleButtons.Add(button);
        }
        foreach (Button button in staleButtons)
            registeredButtons.Remove(button);
    }

    private static void PlayClick()
    {
        PersistentAudioService1912.Instance?.PlayUIClick();
    }
}
