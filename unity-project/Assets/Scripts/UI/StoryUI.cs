using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class StoryUI : MonoBehaviour
{
    private Text bodyText;
    private string fullText;
    private bool skipping;

    public static void Show(string title, string text, string previewResource, Action complete)
    {
        GameObject go = new GameObject("StoryUI", typeof(StoryUI));
        StoryUI ui = go.GetComponent<StoryUI>();
        ui.Build(title, text, previewResource, complete);
    }

    private void Build(string title, string text, string previewResource, Action complete)
    {
        Canvas canvas = UIUtils.CreateCanvas("StoryCanvas", 40);
        UIUtils.Background(canvas.transform, string.IsNullOrEmpty(previewResource) ? "UI/Hero" : previewResource, Color.white);
        UIUtils.Scrim(canvas.transform, "StoryPanel", new Color(0.025f, 0.050f, 0.042f, 0.92f), new Vector2(0f, 0f), new Vector2(0.60f, 1f), Vector2.zero, Vector2.zero);
        UIUtils.Scrim(canvas.transform, "EdgeShade", new Color(0.02f, 0.04f, 0.03f, 0.38f), new Vector2(0.60f, 0f), new Vector2(0.72f, 1f), Vector2.zero, Vector2.zero);
        UIUtils.Panel(canvas.transform, "BrassRule", UITheme.Brass, new Vector2(0.06f, 0.92f), new Vector2(0.54f, 0.925f), Vector2.zero, Vector2.zero, false);
        UIUtils.Text(canvas.transform, "Chapter", "ДОРОГА К МОНАКО  /  АРХИВ ЭКСПЕДИЦИИ", 20, UITheme.Brass, TextAnchor.MiddleLeft, new Vector2(0.06f, 0.84f), new Vector2(0.54f, 0.90f), Vector2.zero, Vector2.zero);
        UIUtils.Text(canvas.transform, "Title", title, 58, UITheme.Cream, TextAnchor.MiddleLeft, new Vector2(0.06f, 0.68f), new Vector2(0.55f, 0.83f), Vector2.zero, Vector2.zero, UITheme.TitleFont);
        UIUtils.Panel(canvas.transform, "Divider", new Color(0.77f, 0.63f, 0.36f, 0.45f), new Vector2(0.06f, 0.635f), new Vector2(0.54f, 0.64f), Vector2.zero, Vector2.zero, false);
        bodyText = UIUtils.Text(canvas.transform, "Body", "", 28, UITheme.Cream, TextAnchor.UpperLeft, new Vector2(0.06f, 0.22f), new Vector2(0.54f, 0.62f), Vector2.zero, Vector2.zero);
        UIUtils.Text(canvas.transform, "Page", "1 / 1", 18, UITheme.Muted, TextAnchor.MiddleRight, new Vector2(0.44f, 0.15f), new Vector2(0.54f, 0.19f), Vector2.zero, Vector2.zero);
        UIUtils.Button(canvas.transform, "Continue", "Продолжить", new Vector2(0.34f, 0.06f), new Vector2(0.54f, 0.14f), Vector2.zero, Vector2.zero, () =>
        {
            if (!skipping)
            {
                skipping = true;
                bodyText.text = fullText;
            }
            else
            {
                Close(complete);
            }
        }, 26);
        UIUtils.Button(canvas.transform, "Skip", "Пропустить", new Vector2(0.06f, 0.06f), new Vector2(0.25f, 0.14f), Vector2.zero, Vector2.zero, () => Close(complete), 24);
        fullText = text;
        StartCoroutine(Typewriter());
    }

    private void Close(Action complete)
    {
        Canvas canvas = GameObject.Find("StoryCanvas")?.GetComponent<Canvas>();
        if (canvas != null) Destroy(canvas.gameObject);
        Destroy(gameObject);
        complete?.Invoke();
    }

    private IEnumerator Typewriter()
    {
        if (bodyText == null)
            yield break;
        for (int i = 0; i <= fullText.Length; i++)
        {
            if (skipping || bodyText == null)
                yield break;
            bodyText.text = fullText.Substring(0, i);
            yield return new WaitForSeconds(0.018f);
        }
    }
}
