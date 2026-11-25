using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class StoryUI : MonoBehaviour
{
    private Text bodyText;
    private string fullText;
    private Action onComplete;
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
        UIUtils.Background(canvas.transform, string.IsNullOrEmpty(previewResource) ? "UI/Hero" : previewResource, new Color(0.25f, 0.30f, 0.27f, 1f));
        UIUtils.Panel(canvas.transform, "Shade", new Color(0.02f, 0.04f, 0.03f, 0.72f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        UIUtils.Text(canvas.transform, "Chapter", "ДОРОГА К МОНАКО", 20, UITheme.Brass, TextAnchor.MiddleLeft, new Vector2(0.08f, 0.78f), new Vector2(0.92f, 0.86f), Vector2.zero, Vector2.zero);
        UIUtils.Text(canvas.transform, "Title", title, 52, UITheme.Cream, TextAnchor.MiddleLeft, new Vector2(0.08f, 0.64f), new Vector2(0.92f, 0.78f), Vector2.zero, Vector2.zero);
        bodyText = UIUtils.Text(canvas.transform, "Body", "", 27, UITheme.Cream, TextAnchor.UpperLeft, new Vector2(0.08f, 0.20f), new Vector2(0.78f, 0.62f), Vector2.zero, Vector2.zero);
        UIUtils.Button(canvas.transform, "Continue", "Продолжить", new Vector2(0.72f, 0.08f), new Vector2(0.92f, 0.17f), Vector2.zero, Vector2.zero, () =>
        {
            if (!skipping)
            {
                skipping = true;
                bodyText.text = fullText;
            }
            else
            {
                Destroy(canvas.gameObject);
                Destroy(gameObject);
                complete?.Invoke();
            }
        });
        UIUtils.Button(canvas.transform, "Skip", "Пропустить", new Vector2(0.08f, 0.08f), new Vector2(0.25f, 0.17f), Vector2.zero, Vector2.zero, () =>
        {
            Destroy(canvas.gameObject);
            Destroy(gameObject);
            complete?.Invoke();
        });
        fullText = text;
        onComplete = complete;
        StartCoroutine(Typewriter());
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
