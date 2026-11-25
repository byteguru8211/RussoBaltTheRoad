using UnityEngine;
using UnityEngine.SceneManagement;

public class ResultsUI : MonoBehaviour
{
    public static void Show(RaceManager race)
    {
        GameObject go = new GameObject("ResultsUI", typeof(ResultsUI));
        go.GetComponent<ResultsUI>().Build(race);
    }

    private void Build(RaceManager race)
    {
        Canvas canvas = UIUtils.CreateCanvas("ResultsCanvas", 60);
        UIUtils.Background(canvas.transform, race.TrackData.previewResource, new Color(0.22f, 0.27f, 0.24f, 1f));
        UIUtils.Panel(canvas.transform, "Shade", new Color(0.01f, 0.02f, 0.015f, 0.78f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        string headline = race.PlayerWon ? "Победа" : race.PlayerFinished ? "Финиш" : "Маршрут не завершён";
        UIUtils.Text(canvas.transform, "Title", headline, 64, UITheme.Cream, TextAnchor.MiddleCenter, new Vector2(0.20f, 0.70f), new Vector2(0.80f, 0.84f), Vector2.zero, Vector2.zero);
        UIUtils.Text(canvas.transform, "Stats", $"Место: {race.CurrentPosition}/{race.RacersCount}\nВремя: {RaceHUD.FormatTime(race.Elapsed)}\nНаграда: {race.Reward} рублей\nПройдено: {race.Player.distanceSinceReset / 1000f:F2} км", 30, UITheme.Cream, TextAnchor.MiddleCenter, new Vector2(0.25f, 0.42f), new Vector2(0.75f, 0.68f), Vector2.zero, Vector2.zero);
        UIUtils.Button(canvas.transform, "Retry", "Ещё раз", new Vector2(0.28f, 0.22f), new Vector2(0.46f, 0.32f), Vector2.zero, Vector2.zero, () => SceneManager.LoadScene(SceneManager.GetActiveScene().name));
        UIUtils.Button(canvas.transform, "Menu", "Экспедиция", new Vector2(0.54f, 0.22f), new Vector2(0.72f, 0.32f), Vector2.zero, Vector2.zero, () => SceneManager.LoadScene("Garage"));
    }
}
