using System;
using System.Text;
using Unity.Collections;
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
    private InputField nameField;
    private InputField lobbyNameField;
    private Text statusText;
    private Text rosterText;
    private Text lobbyStatus;
    private Text addressText;
    private Text selectionText;
    private Text connectionText;
    private Button readyButton;
    private Button startButton;
    private Button hostButton;
    private Button joinButton;
    private bool ready;
    private float refreshTimer;
    private bool connecting;
    private float connectStarted;

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
        UIUtils.Scrim(canvas.transform, "NetworkShade", new Color(0.006f, 0.016f, 0.012f, 0.94f), Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        connectionText = UIUtils.Text(canvas.transform, "Connection", "ПОДКЛЮЧЕНИЕ К ЛОББИ…", 28, UITheme.Brass, TextAnchor.MiddleCenter, new Vector2(0.30f, 0.45f), new Vector2(0.70f, 0.55f), Vector2.zero, Vector2.zero, UITheme.TitleFont);
        connectionText.gameObject.SetActive(false);
        entryPanel = UIUtils.Card(canvas.transform, "NetworkEntry", UITheme.Dark, new Vector2(0.18f, 0.10f), new Vector2(0.82f, 0.90f), Vector2.zero, Vector2.zero).rectTransform;
        UIUtils.Panel(entryPanel, "TopRule", UITheme.Brass, new Vector2(0.06f, 0.93f), new Vector2(0.94f, 0.935f), Vector2.zero, Vector2.zero, false);
        UIUtils.Text(entryPanel, "Title", "Сетевая игра", 48, UITheme.Cream, TextAnchor.MiddleLeft, new Vector2(0.07f, 0.84f), new Vector2(0.93f, 0.92f), Vector2.zero, Vector2.zero, UITheme.TitleFont);
        UIUtils.Text(entryPanel, "Subtitle", "LAN  ·  RadminVPN  ·  прямой IP  ·  UDP 7777", 20, UITheme.Brass, TextAnchor.MiddleLeft, new Vector2(0.07f, 0.79f), new Vector2(0.93f, 0.84f), Vector2.zero, Vector2.zero);
        selectionText = UIUtils.Text(entryPanel, "Selection", "", 18, UITheme.Muted, TextAnchor.MiddleLeft, new Vector2(0.07f, 0.735f), new Vector2(0.93f, 0.785f), Vector2.zero, Vector2.zero);
        RefreshSelectionLabel();
        UIUtils.Text(entryPanel, "NameLabel", "ИМЯ ГОНЩИКА", 16, UITheme.Muted, TextAnchor.MiddleLeft, new Vector2(0.07f, 0.675f), new Vector2(0.45f, 0.715f), Vector2.zero, Vector2.zero);
        nameField = CreateInput(entryPanel, "Name", GameManager.Instance.Profile.playerName, new Vector2(0.07f, 0.615f), new Vector2(0.45f, 0.67f), SetLocalName);
        UIUtils.Text(entryPanel, "AddressLabel", "IP ИЛИ ИМЯ ХОСТА", 16, UITheme.Muted, TextAnchor.MiddleLeft, new Vector2(0.52f, 0.675f), new Vector2(0.93f, 0.715f), Vector2.zero, Vector2.zero);
        addressField = CreateInput(entryPanel, "Address", PlayerPrefs.GetString("russo_balt_lan_address", "127.0.0.1"), new Vector2(0.52f, 0.615f), new Vector2(0.93f, 0.67f), value => PlayerPrefs.SetString("russo_balt_lan_address", value.Trim()));
        hostButton = UIUtils.Button(entryPanel, "Host", "Создать игру", new Vector2(0.07f, 0.47f), new Vector2(0.45f, 0.56f), Vector2.zero, Vector2.zero, Host, 26);
        joinButton = UIUtils.Button(entryPanel, "Join", "Подключиться", new Vector2(0.52f, 0.47f), new Vector2(0.93f, 0.56f), Vector2.zero, Vector2.zero, Join, 26);
        statusText = UIUtils.Text(entryPanel, "Status", "Хост: создайте игру и сообщите игрокам свой RadminVPN IP.\nКлиент: введите адрес хоста. Все пилоты отмечаются готовыми до старта.", 19, UITheme.Muted, TextAnchor.UpperLeft, new Vector2(0.08f, 0.25f), new Vector2(0.92f, 0.42f), Vector2.zero, Vector2.zero);
        UIUtils.Text(entryPanel, "Hint", "Максимум 6 пилотов  ·  Порт 7777  ·  Имя сохраняется в профиле", 16, UITheme.Muted, TextAnchor.MiddleLeft, new Vector2(0.08f, 0.17f), new Vector2(0.92f, 0.22f), Vector2.zero, Vector2.zero);
        UIUtils.Button(entryPanel, "Back", "Назад", new Vector2(0.35f, 0.045f), new Vector2(0.65f, 0.13f), Vector2.zero, Vector2.zero, Leave, 23);
    }

    private InputField CreateInput(Transform parent, string name, string value, Vector2 min, Vector2 max, Action<string> changed)
    {
        InputField field = UIUtils.InputField(parent, name, value, Vector2.zero, changed);
        RectTransform rect = field.GetComponent<RectTransform>();
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return field;
    }

    private void RefreshSelectionLabel()
    {
        if (selectionText == null) return;
        string trackId = LobbyManager.Instance != null ? LobbyManager.Instance.TrackId.Value.ToString() : GameManager.Instance.SelectedTrackId;
        RaceType raceType = LobbyManager.Instance != null ? (RaceType)LobbyManager.Instance.RaceType.Value : GameManager.Instance.SelectedRaceType;
        TrackData track = GameManager.LoadTrack(trackId);
        selectionText.text = $"ЗАЕЗД  {RaceTypeInfo.DisplayName(raceType).ToUpperInvariant()}   ·   {(track != null ? track.displayName : "Маршрут не выбран")}";
    }

    private void BeginConnection()
    {
        connecting = true;
        connectStarted = Time.unscaledTime;
        connectionText.text = "ПОДКЛЮЧЕНИЕ К ЛОББИ…";
        connectionText.gameObject.SetActive(true);
        entryPanel.gameObject.SetActive(false);
        hostButton.interactable = false;
        joinButton.interactable = false;
        refreshTimer = 0f;
    }

    private void FailConnection(string message)
    {
        connecting = false;
        connectionText.gameObject.SetActive(false);
        if (lobbyPanel != null)
        {
            Destroy(lobbyPanel.gameObject);
            lobbyPanel = null;
        }
        if (entryPanel != null) entryPanel.gameObject.SetActive(true);
        if (hostButton != null) hostButton.interactable = true;
        if (joinButton != null) joinButton.interactable = true;
        SetEntryStatus(message, true);
    }

    private void SetEntryStatus(string message, bool error)
    {
        if (statusText == null) return;
        statusText.text = message;
        statusText.color = error ? UITheme.Danger : UITheme.Muted;
    }

    private void SetLocalName(string value)
    {
        string clean = TrimName(value);
        if (string.IsNullOrWhiteSpace(clean)) clean = "Гонщик";
        GameManager.Instance.Profile.playerName = clean;
        GameManager.Instance.Save();
        if (nameField != null && nameField.text != clean) nameField.text = clean;
        if (lobbyNameField != null && lobbyNameField.text != clean) lobbyNameField.text = clean;
        if (LobbyManager.Instance != null)
            LobbyManager.Instance.SetNameServerRpc(new FixedString32Bytes(clean));
    }

    private static string TrimName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "Гонщик";
        string clean = value.Replace("\n", " ").Replace("\r", " ").Trim();
        while (clean.Length > 1 && Encoding.UTF8.GetByteCount(clean) > 31)
            clean = clean.Substring(0, clean.Length - 1);
        return clean;
    }

    private void EnsureSession()
    {
        if (LanSessionController.Instance == null)
            new GameObject("LanSessionController").AddComponent<LanSessionController>();
    }

    private void Host()
    {
        if (!GameManager.Instance.CanStartRace())
        {
            SetEntryStatus("Сначала откройте маршрут и выберите автомобиль.", true);
            return;
        }
        SetLocalName(nameField.text);
        EnsureSession();
        BeginConnection();
        LanSessionController.Instance.Host("127.0.0.1");
        if (!LanSessionController.Instance.Network.IsListening)
            FailConnection("Не удалось открыть LAN-соединение.");
    }

    private void Join()
    {
        string address = addressField.text.Trim();
        UriHostNameType addressType = Uri.CheckHostName(address);
        if (addressType == UriHostNameType.Unknown || address.Contains(" "))
        {
            SetEntryStatus("Введите корректный IPv4, IPv6 или адрес RadminVPN.", true);
            addressField.Select();
            return;
        }
        SetLocalName(nameField.text);
        PlayerPrefs.SetString("russo_balt_lan_address", address);
        PlayerPrefs.Save();
        EnsureSession();
        BeginConnection();
        LanSessionController.Instance.Join(address);
        if (!LanSessionController.Instance.Network.IsListening)
            FailConnection("Клиент не запустился. Проверьте адрес и порт 7777.");
    }

    private void Update()
    {
        LanSessionController session = LanSessionController.Instance;
        if (session == null) return;
        if (connecting)
        {
            if (LobbyManager.Instance != null && SceneManager.GetActiveScene().name == "Lobby")
            {
                connecting = false;
                connectionText.gameObject.SetActive(false);
                entryPanel.gameObject.SetActive(false);
                if (lobbyPanel == null) BuildLobby();
            }
            else if (!session.Network.IsListening)
            {
                FailConnection("Соединение закрыто. Проверьте адрес хоста и повторите.");
            }
            else if (Time.unscaledTime - connectStarted > 12f)
            {
                session.Network.Shutdown();
                FailConnection("Хост не ответил за 12 секунд. Проверьте RadminVPN, IP и порт 7777.");
            }
        }
        if (!session.Network.IsListening) return;
        if (SceneManager.GetActiveScene().name.StartsWith("Race_", StringComparison.Ordinal))
        {
            HideForRace();
            return;
        }
        if (SceneManager.GetActiveScene().name != "Lobby") return;
        if (lobbyPanel == null) BuildLobby();
        refreshTimer += Time.unscaledTime;
        if (refreshTimer < 0.35f) return;
        refreshTimer = 0f;
        RefreshLobby();
    }

    private void BuildLobby()
    {
        lobbyPanel = UIUtils.Card(canvas.transform, "LobbyPanel", UITheme.Dark, new Vector2(0.12f, 0.07f), new Vector2(0.88f, 0.93f), Vector2.zero, Vector2.zero).rectTransform;
        UIUtils.Text(lobbyPanel, "Title", "LAN · Лобби", 46, UITheme.Cream, TextAnchor.MiddleLeft, new Vector2(0.05f, 0.86f), new Vector2(0.95f, 0.94f), Vector2.zero, Vector2.zero, UITheme.TitleFont);
        addressText = UIUtils.Text(lobbyPanel, "Address", "", 18, UITheme.Brass, TextAnchor.MiddleLeft, new Vector2(0.05f, 0.80f), new Vector2(0.95f, 0.85f), Vector2.zero, Vector2.zero);
        selectionText = UIUtils.Text(lobbyPanel, "Selection", "", 18, UITheme.Muted, TextAnchor.MiddleLeft, new Vector2(0.05f, 0.75f), new Vector2(0.95f, 0.80f), Vector2.zero, Vector2.zero);
        RefreshSelectionLabel();
        rosterText = UIUtils.Text(lobbyPanel, "Roster", "Ожидание списка пилотов…", 24, UITheme.Cream, TextAnchor.UpperLeft, new Vector2(0.06f, 0.25f), new Vector2(0.94f, 0.72f), Vector2.zero, Vector2.zero);
        UIUtils.Text(lobbyPanel, "NameLabel", "ИМЯ В ЛОББИ", 14, UITheme.Muted, TextAnchor.MiddleLeft, new Vector2(0.06f, 0.205f), new Vector2(0.35f, 0.24f), Vector2.zero, Vector2.zero);
        lobbyNameField = CreateInput(lobbyPanel, "Name", GameManager.Instance.Profile.playerName, new Vector2(0.06f, 0.15f), new Vector2(0.35f, 0.20f), SetLocalName);
        lobbyStatus = UIUtils.Text(lobbyPanel, "Status", "", 16, UITheme.Muted, TextAnchor.MiddleLeft, new Vector2(0.38f, 0.15f), new Vector2(0.62f, 0.20f), Vector2.zero, Vector2.zero);
        readyButton = UIUtils.Button(lobbyPanel, "Ready", "Отметить готовым", new Vector2(0.06f, 0.035f), new Vector2(0.28f, 0.115f), Vector2.zero, Vector2.zero, ToggleReady, 22);
        startButton = UIUtils.Button(lobbyPanel, "Start", "Начать гонку", new Vector2(0.31f, 0.035f), new Vector2(0.61f, 0.115f), Vector2.zero, Vector2.zero, StartRace, 22);
        UIUtils.Button(lobbyPanel, "Leave", "Выйти", new Vector2(0.65f, 0.035f), new Vector2(0.94f, 0.115f), Vector2.zero, Vector2.zero, Leave, 22);
        RefreshLobby();
    }

    private void RefreshLobby()
    {
        LanSessionController session = LanSessionController.Instance;
        if (session == null) return;
        addressText.text = session.IsHost ? $"ХОСТ  {session.LocalAddress}:{session.Port}   ·   сообщите игрокам RadminVPN IP" : $"ПОДКЛЮЧЕНИЕ  {addressField.text.Trim()}:{session.Port}";
        if (LobbyManager.Instance == null)
        {
            rosterText.text = "Синхронизация лобби…";
            lobbyStatus.text = "Ожидание данных сервера";
            lobbyStatus.color = UITheme.Muted;
            return;
        }
        string text = "";
        int readyCount = 0;
        bool localReady = false;
        foreach (LobbyEntry entry in LobbyManager.Instance.Entries)
        {
            if (entry.Ready) readyCount++;
            if (entry.ClientId == session.Network.LocalClientId) localReady = entry.Ready;
            text += $"{(entry.Ready ? "◆" : "◇")}  {entry.Name}  ·  {(entry.ClientId == 0 ? "хост" : "пилот")}\n";
        }
        ready = localReady;
        rosterText.text = string.IsNullOrEmpty(text) ? "Пилоты ещё не подключились…" : text.TrimEnd();
        bool allReady = LobbyManager.Instance.Entries.Count > 0 && readyCount == LobbyManager.Instance.Entries.Count;
        startButton.gameObject.SetActive(session.IsHost);
        startButton.interactable = allReady;
        startButton.transform.Find("Label").GetComponent<Text>().text = allReady ? "Начать гонку" : $"Ждём готовности {readyCount}/{LobbyManager.Instance.Entries.Count}";
        readyButton.transform.Find("Label").GetComponent<Text>().text = ready ? "Готов ✓" : "Отметить готовым";
        lobbyStatus.text = session.IsHost ? (allReady ? "Все пилоты готовы" : "Старт доступен после готовности всех") : (ready ? "Ожидание хоста" : "Отметьтесь готовыми");
        lobbyStatus.color = allReady ? UITheme.Success : UITheme.Muted;
    }

    private void ToggleReady()
    {
        if (LobbyManager.Instance == null) return;
        ready = !ready;
        LobbyManager.Instance.ToggleReadyServerRpc(ready);
        RefreshLobby();
    }

    private void StartRace()
    {
        if (LobbyManager.Instance == null || !LanSessionController.Instance.IsHost) return;
        bool allReady = LobbyManager.Instance.Entries.Count > 0;
        foreach (LobbyEntry entry in LobbyManager.Instance.Entries)
            if (!entry.Ready) allReady = false;
        if (!allReady)
        {
            lobbyStatus.text = "Все пилоты должны отметиться готовыми";
            lobbyStatus.color = UITheme.Danger;
            return;
        }
        lobbyStatus.text = "Синхронизация старта…";
        lobbyStatus.color = UITheme.Brass;
        LobbyManager.Instance.StartRaceServerRpc();
    }

    private void Leave()
    {
        if (LanSessionController.Instance != null && LanSessionController.Instance.Network.IsListening)
            LanSessionController.Instance.ShutdownSession();
        Close();
    }

    public void HideForRace()
    {
        if (Instance == this) Instance = null;
        Destroy(gameObject);
    }

    private void Close()
    {
        if (Instance == this) Instance = null;
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }
}
