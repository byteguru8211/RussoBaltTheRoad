using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class ResultsUI : MonoBehaviour
{
    public static void Show(RaceManager race)
    {
        if (race == null) return;
        GameObject go = new GameObject("ResultsUI", typeof(ResultsUI));
        go.GetComponent<ResultsUI>().Build(race);
    }

    private void Build(RaceManager race)
    {
        Canvas canvas = UIUtils.CreateCanvas("ResultsCanvas", 60);
        UIUtils.Background(canvas.transform, race.TrackData.previewResource, new Color(0.22f, 0.27f, 0.24f, 1f));
        UIUtils.Panel(canvas.transform, "Shade", new Color(0.008f, 0.018f, 0.014f, 0.86f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        UIUtils.Panel(canvas.transform, "TopRule", UITheme.Brass, new Vector2(0.05f, 0.925f), new Vector2(0.95f, 0.929f), Vector2.zero, Vector2.zero, false);
        string headline = race.PlayerWon ? "ПОБЕДА" : race.PlayerFinished ? "ФИНИШ" : "МАРШРУТ НЕ ЗАВЕРШЁН";
        UIUtils.Text(canvas.transform, "Kicker", "RUSSO-BALT  /  RACE CONTROL  /  1912", 17, UITheme.Brass, TextAnchor.MiddleCenter, new Vector2(0.20f, 0.865f), new Vector2(0.80f, 0.905f), Vector2.zero, Vector2.zero);
        UIUtils.Text(canvas.transform, "Title", headline, 62, race.PlayerWon ? UITheme.Brass : UITheme.Cream, TextAnchor.MiddleCenter, new Vector2(0.20f, 0.76f), new Vector2(0.80f, 0.865f), Vector2.zero, Vector2.zero, UITheme.TitleFont);
        UIUtils.Text(canvas.transform, "Subtitle", $"{race.TrackData.displayName}  ·  {RaceTypeInfo.DisplayName(GameManager.Instance.SelectedRaceType)}", 21, UITheme.Muted, TextAnchor.MiddleCenter, new Vector2(0.20f, 0.715f), new Vector2(0.80f, 0.76f), Vector2.zero, Vector2.zero);

        UIUtils.Card(canvas.transform, "StandingsCard", UITheme.Dark, new Vector2(0.05f, 0.18f), new Vector2(0.58f, 0.69f), Vector2.zero, Vector2.zero);
        UIUtils.Text(canvas.transform, "StandingsTitle", "ИТОГОВЫЙ ПОРЯДОК", 18, UITheme.Brass, TextAnchor.MiddleLeft, new Vector2(0.075f, 0.635f), new Vector2(0.55f, 0.675f), Vector2.zero, Vector2.zero);
        List<RaceStandings.Standing> rows = RaceStandings.Build(race);
        for (int i = 0; i < rows.Count; i++)
        {
            RaceStandings.Standing row = rows[i];
            float top = 0.625f - i * 0.061f;
            Image rowPanel = UIUtils.Panel(canvas.transform, "ResultRow" + i, row.IsPlayer ? UITheme.Raised : UITheme.Panel, new Vector2(0.07f, top - 0.054f), new Vector2(0.56f, top - 0.003f), Vector2.zero, Vector2.zero, false);
            UIUtils.Text(rowPanel.transform, "Place", row.Place.ToString(), 21, UITheme.Brass, TextAnchor.MiddleCenter, new Vector2(0.01f, 0f), new Vector2(0.08f, 1f), Vector2.zero, Vector2.zero);
            UIUtils.Text(rowPanel.transform, "Name", row.Name, 22, row.IsPlayer ? UITheme.Brass : UITheme.Cream, TextAnchor.MiddleLeft, new Vector2(0.10f, 0f), new Vector2(0.73f, 1f), new Vector2(0f, 0f), new Vector2(-8f, 0f), UITheme.TitleFont);
            UIUtils.Text(rowPanel.transform, "Gap", row.Place == 1 ? "ЛИДЕР" : $"+{Mathf.RoundToInt(row.GapMeters)} м", 16, UITheme.Muted, TextAnchor.MiddleRight, new Vector2(0.73f, 0f), new Vector2(0.98f, 1f), Vector2.zero, Vector2.zero);
        }

        RaceStandings.Standing player = RaceStandings.FindPlayer(rows);
        int position = player != null ? player.Place : race.CurrentPosition;
        float best = GameManager.Instance.GetBestTime(race.TrackData.id);
        UIUtils.Card(canvas.transform, "StatsCard", UITheme.Dark, new Vector2(0.61f, 0.18f), new Vector2(0.95f, 0.69f), Vector2.zero, Vector2.zero);
        UIUtils.Text(canvas.transform, "StatsTitle", "ПРОТОКОЛ ЗАЕЗДА", 18, UITheme.Brass, TextAnchor.MiddleLeft, new Vector2(0.635f, 0.635f), new Vector2(0.925f, 0.675f), Vector2.zero, Vector2.zero);
        UIUtils.Text(canvas.transform, "Position", $"{position}/{Mathf.Max(1, race.RacersCount)}", 58, UITheme.Cream, TextAnchor.MiddleCenter, new Vector2(0.63f, 0.52f), new Vector2(0.93f, 0.625f), Vector2.zero, Vector2.zero, UITheme.TitleFont);
        UIUtils.Text(canvas.transform, "PositionLabel", "МЕСТО", 15, UITheme.Muted, TextAnchor.MiddleCenter, new Vector2(0.63f, 0.485f), new Vector2(0.93f, 0.52f), Vector2.zero, Vector2.zero);
        UIUtils.Text(canvas.transform, "Stats", $"Время          {RaceHUD.FormatTime(race.Elapsed)}\nЛучшее       {(best > 0f ? RaceHUD.FormatTime(best) : "—:—.—")}\nНаграда     {race.Reward} рублей\nПройдено   {(race.Player != null ? race.Player.distanceSinceReset / 1000f : 0f):F2} км", 22, UITheme.Cream, TextAnchor.UpperLeft, new Vector2(0.64f, 0.215f), new Vector2(0.92f, 0.47f), Vector2.zero, Vector2.zero);

        bool networkRace = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        UIUtils.Button(canvas.transform, "Retry", networkRace ? "Выйти в меню" : "Ещё раз", new Vector2(0.12f, 0.07f), new Vector2(0.32f, 0.15f), Vector2.zero, Vector2.zero, () =>
        {
            Time.timeScale = 1f;
            if (networkRace)
                LanSessionController.Instance.ShutdownSession();
            else
                SceneManager.LoadScene("PreRace");
        }, 25);
        UIUtils.Button(canvas.transform, "Garage", "Гараж", new Vector2(0.36f, 0.07f), new Vector2(0.54f, 0.15f), Vector2.zero, Vector2.zero, () =>
        {
            Time.timeScale = 1f;
            if (LanSessionController.Instance != null && LanSessionController.Instance.Network.IsListening)
                LanSessionController.Instance.ShutdownSession();
            else
                SceneManager.LoadScene("Garage");
        }, 25);
        UIUtils.Button(canvas.transform, "Menu", "Главное меню", new Vector2(0.58f, 0.07f), new Vector2(0.82f, 0.15f), Vector2.zero, Vector2.zero, () =>
        {
            Time.timeScale = 1f;
            race.ReturnToMenu();
        }, 25);
    }
}
