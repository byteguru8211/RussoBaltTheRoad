using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class NetworkMenuUI : MonoBehaviour
{
    public static NetworkMenuUI Instance { get; private set; }
    private Canvas canvas;
    private RectTransform entryPanel;
    private RectTransform lobbyPanel;
    private InputField addressField;
    private Text statusText;
    private Text rosterText;
    private Button readyButton;
    private Button startButton;
    private bool ready;
    private float refreshTimer;

    public static void Show(Canvas parent)
    {
        if (Instance != null)
        {
            Destroy(Instance.gameObject);
            Instance = null;
        }
        GameObject go = new GameObject("NetworkMenuUI", typeof(NetworkMenuUI));
        DontDestroyOnLoad(go);
        Instance = go.GetComponent<NetworkMenuUI>();
        Instance.canvas = UIUtils.CreateCanvas("NetworkCanvas", 60);
        Instance.canvas.transform.SetParent(go.transform, false);
        Instance.Build();
    }

    private void Build()
    {
        UIUtils.Scrim(canvas.transform, "NetworkShade", new Color(0.01f, 0.02f, 0.015f, 0.90f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        entryPanel = UIUtils.Card(canvas.transform, "NetworkEntry", UITheme.Dark, new Vector2(0.22f, 0.16f), new Vector2(0.78f, 0.84f), Vector2.zero, Vector2.zero).rectTransform;
        UIUtils.Text(entryPanel, "Title", "Сетевая игра", 50, UITheme.Cream, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(46, -76), new Vector2(-46, -18), UITheme.TitleFont);
        UIUtils.Text(entryPanel, "Subtitle", "LAN · RadminVPN · прямой IP", 22, UITheme.Brass, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(46, -126), new Vector2(-46, -96));
        UIUtils.Text(entryPanel, "AddressLabel", "IP хоста", 21, UITheme.Muted, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(0, 1), new Vector2(46, -190), new Vector2(260, -162));
        addressField = UIUtils.InputField(entryPanel, "Address", "127.0.0.1", new Vector2(300, -212), v => { });
        UIUtils.Button(entryPanel, "Host", "Создать игру", new Vector2(0.10f, 0.30f), new Vector2(0.46f, 0.41f), Vector2.zero, Vector2.zero, Host, 27);
        UIUtils.Button(entryPanel, "Join", "Подключиться", new Vector2(0.54f, 0.30f), new Vector2(0.90f, 0.41f), Vector2.zero, Vector2.zero, Join, 27);
        statusText = UIUtils.Text(entryPanel, "Status", "Хост: создайте игру и сообщите RadminVPN IP.\nКлиент: введите IP хоста. UDP порт 7777.", 20, UITheme.Muted, TextAnchor.UpperLeft, new Vector2(0.08f, 0.10f), new Vector2(0.92f, 0.27f), Vector2.zero, Vector2.zero);
        UIUtils.Button(entryPanel, "Back", "Назад", new Vector2(0.36f, 0.02f), new Vector2(0.64f, 0.10f), Vector2.zero, Vector2.zero, Leave, 23);
    }

    private void EnsureSession()
    {
        if (LanSessionController.Instance == null)
            new GameObject("LanSessionController").AddComponent<LanSessionController>();
    }

    private void Host()
    {
        EnsureSession();
        entryPanel.gameObject.SetActive(false);
        LanSessionController.Instance.Host("127.0.0.1");
    }

    private void Join()
    {
        EnsureSession();
        entryPanel.gameObject.SetActive(false);
        LanSessionController.Instance.Join(addressField.text);
    }

    private void Update()
    {
        if (LanSessionController.Instance == null || !LanSessionController.Instance.Network.IsListening) return;
        if (SceneManager.GetActiveScene().name.StartsWith("Race_"))
        {
            HideForRace();
            return;
        }
        if (SceneManager.GetActiveScene().name != "Lobby") return;
        if (lobbyPanel == null) BuildLobby();
        refreshTimer += Time.deltaTime;
        if (refreshTimer < 0.35f) return;
        refreshTimer = 0f;
        RefreshLobby();
    }

    private void BuildLobby()
    {
        lobbyPanel = UIUtils.Card(canvas.transform, "LobbyPanel", UITheme.Dark, new Vector2(0.14f, 0.08f), new Vector2(0.86f, 0.92f), Vector2.zero, Vector2.zero).rectTransform;
        UIUtils.Text(lobbyPanel, "Title", "Лобби экспедиции", 48, UITheme.Cream, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(44, -74), new Vector2(-44, -18), UITheme.TitleFont);
        UIUtils.Text(lobbyPanel, "Address", "", 21, UITheme.Brass, TextAnchor.MiddleLeft, new Vector2(0, 1), new Vector2(1, 1), new Vector2(44, -120), new Vector2(-44, -92));
        rosterText = UIUtils.Text(lobbyPanel, "Roster", "", 26, UITheme.Cream, TextAnchor.UpperLeft, new Vector2(0.05f, 0.16f), new Vector2(0.95f, 0.78f), Vector2.zero, Vector2.zero);
        readyButton = UIUtils.Button(lobbyPanel, "Ready", "Готов", new Vector2(0.08f, 0.03f), new Vector2(0.30f, 0.12f), Vector2.zero, Vector2.zero, ToggleReady, 26);
        startButton = UIUtils.Button(lobbyPanel, "Start", "Начать гонку", new Vector2(0.54f, 0.03f), new Vector2(0.78f, 0.12f), Vector2.zero, Vector2.zero, StartRace, 26);
        UIUtils.Button(lobbyPanel, "Leave", "Выйти", new Vector2(0.80f, 0.03f), new Vector2(0.94f, 0.12f), Vector2.zero, Vector2.zero, Leave, 23);
        RefreshLobby();
    }

    private void RefreshLobby()
    {
        if (LobbyManager.Instance == null) return;
        string text = "";
        foreach (LobbyEntry entry in LobbyManager.Instance.Entries)
            text += $"{(entry.Ready ? "◆" : "◇")}  {entry.Name}  ·  {(entry.ClientId == 0 ? "хост" : "пилот")}\n";
        rosterText.text = text;
        startButton.gameObject.SetActive(LanSessionController.Instance.IsHost);
        readyButton.transform.Find("Label").GetComponent<Text>().text = ready ? "Готов" : "Отметить готовым";
    }

    private void ToggleReady()
    {
        if (LobbyManager.Instance == null) return;
        ready = !ready;
        LobbyManager.Instance.ToggleReadyServerRpc(ready);
    }

    private void StartRace()
    {
        if (LobbyManager.Instance != null && LanSessionController.Instance.IsHost)
            LobbyManager.Instance.StartRaceServerRpc();
    }

    private void Leave()
    {
        if (LanSessionController.Instance != null) LanSessionController.Instance.ShutdownSession();
        HideForRace();
    }

    public void HideForRace()
    {
        if (Instance == this) Instance = null;
        Destroy(gameObject);
    }
}
