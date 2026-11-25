using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PauseMenuUI : MonoBehaviour
{
    public static void Show(RaceManager race)
    {
        GameObject go = new GameObject("PauseMenuUI", typeof(PauseMenuUI));
        PauseMenuUI ui = go.GetComponent<PauseMenuUI>();
        ui.Build(race);
    }

    private void Build(RaceManager race)
    {
        bool networkRace = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (!networkRace)
            Time.timeScale = 0f;
        else if (race.Player != null)
        {
            VehicleInput1912 input = race.Player.GetComponent<VehicleInput1912>();
            if (input != null) input.enabled = false;
        }
        Canvas canvas = UIUtils.CreateCanvas("PauseCanvas", 50);
        UIUtils.Panel(canvas.transform, "Shade", new Color(0.01f, 0.02f, 0.015f, 0.82f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        UIUtils.Text(canvas.transform, "Title", "Пауза", 58, UITheme.Cream, TextAnchor.MiddleCenter, new Vector2(0.25f, 0.68f), new Vector2(0.75f, 0.82f), Vector2.zero, Vector2.zero);
        UIUtils.Button(canvas.transform, "Resume", "Продолжить", new Vector2(0.35f, 0.48f), new Vector2(0.65f, 0.58f), Vector2.zero, Vector2.zero, () =>
        {
            Time.timeScale = 1f;
            if (networkRace && race.Player != null)
            {
                VehicleInput1912 input = race.Player.GetComponent<VehicleInput1912>();
                if (input != null) input.enabled = true;
            }
            Destroy(canvas.gameObject);
            Destroy(gameObject);
        });
        UIUtils.Button(canvas.transform, "Restart", "Начать заново", new Vector2(0.35f, 0.35f), new Vector2(0.65f, 0.45f), Vector2.zero, Vector2.zero, () =>
        {
            Time.timeScale = 1f;
            if (networkRace)
                LanSessionController.Instance.ShutdownSession();
            else
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        });
        UIUtils.Button(canvas.transform, "Menu", "В главное меню", new Vector2(0.35f, 0.22f), new Vector2(0.65f, 0.32f), Vector2.zero, Vector2.zero, () =>
        {
            Time.timeScale = 1f;
            race.ReturnToMenu();
        });
    }
}
