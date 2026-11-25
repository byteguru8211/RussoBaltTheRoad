using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class RaceManager : MonoBehaviour
{
    public enum RaceState { Intro, Countdown, Racing, Finished }
    public static RaceManager Instance { get; private set; }

    public TrackData TrackData { get; private set; }
    public TrackBuilder TrackBuilder { get; private set; }
    public VehicleController1912 Player { get; private set; }
    public RaceHUD HUD { get; private set; }
    public List<AIDriver1912> AI { get; private set; } = new List<AIDriver1912>();
    public RaceState State { get; private set; }
    public float Elapsed { get; private set; }
    public int CheckpointIndex { get; private set; }
    public int CompletedLaps { get; private set; }
    public int CurrentPosition { get; private set; } = 1;
    public int RacersCount
    {
        get
        {
            PruneNetworkCars();
            return networkMode && networkCars.Count > 0 ? networkCars.Count : AI.Count + 1;
        }
    }
    public int Reward { get; private set; }
    public bool PlayerFinished { get; private set; }
    public bool PlayerWon { get; private set; }
    public string NextInstruction { get; private set; } = "Прямо";
    public float PlayerRaceProgress => playerContinuousProgress;

    private bool networkMode;
    private float playerContinuousProgress;
    private float lastPlayerTrackProgress = -1f;
    private readonly List<NetworkPlayerCar> networkCars = new List<NetworkPlayerCar>();

    private void Awake()
    {
        Instance = this;
        EnsureServices();
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && LobbyManager.Instance != null)
        {
            GameManager.Instance.SelectedTrackId = LobbyManager.Instance.TrackId.Value.ToString();
            GameManager.Instance.SelectedRaceType = (RaceType)LobbyManager.Instance.RaceType.Value;
        }
        else
        {
            string sceneTrackId = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name switch
            {
                "Race_Alps" => "alps_pass",
                "Race_Monaco" => "monaco_finish",
                "Race_Moscow" => "moscow_highway",
                "Race_Finland" => "finland_coast",
                _ => "winter_departure"
            };
            GameManager.Instance.SelectedTrackId = sceneTrackId;
        }
        TrackData = Resources.Load<TrackData>($"Tracks/{GameManager.Instance.SelectedTrackId}");
        if (TrackData == null)
            TrackData = Resources.Load<TrackData>("Tracks/winter_departure");
        GameObject trackRoot = new GameObject("Track");
        TrackBuilder = trackRoot.AddComponent<TrackBuilder>();
        TrackBuilder.Build(TrackData);
        foreach (Checkpoint checkpoint in TrackBuilder.GetComponentsInChildren<Checkpoint>())
            checkpoint.raceManager = this;
        GameManager.Instance.Profile.racesStarted++;
        networkMode = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (networkMode)
            StartCoroutine(SetupNetworkRace());
        else
        {
            SpawnRacers();
            BeginAfterSetup();
        }
    }

    private void EnsureServices()
    {
        if (GameManager.Instance == null)
            new GameObject("GameManager").AddComponent<GameManager>();
        if (AchievementSystem.Instance == null)
            new GameObject("AchievementSystem").AddComponent<AchievementSystem>();
    }

    private void SpawnRacers()
    {
        string vehicleId = GameManager.Instance.SelectedVehicleId;
        GameObject playerPrefab = Resources.Load<GameObject>($"Vehicles/{vehicleId}");
        if (playerPrefab == null)
            playerPrefab = Resources.Load<GameObject>("Vehicles/russo_balt_c24_55");
        VehicleController1912 playerVehicle = playerPrefab != null ? Instantiate(playerPrefab).GetComponent<VehicleController1912>() : null;
        if (playerVehicle != null)
        {
            Player = playerVehicle;
            Player.isPlayer = true;
            Player.SetTransmissionMode(GameManager.Instance.Settings.automaticTransmission);
            Player.transform.position = TrackBuilder.StartPoint - TrackBuilder.StartTangent * 2.5f + Vector3.up * 0.75f;
            Player.transform.rotation = Quaternion.LookRotation(TrackBuilder.StartTangent, Vector3.up);
            Player.SetSurfaceGrip(TrackData.surfaceGrip);
            PrepareLocalGrid(Player);
        }

        string[] rivals = { "benz_1912_racer_concept", "ford_model_t_1912", "fiat_s76_1910", "mercedes_37_90_1912" };
        int aiCount = GameManager.Instance.GetRaceAiCount();
        for (int i = 0; i < aiCount; i++)
        {
            GameObject prefab = Resources.Load<GameObject>($"Vehicles/{rivals[i % rivals.Length]}");
            VehicleController1912 vehicle = prefab != null ? Instantiate(prefab).GetComponent<VehicleController1912>() : null;
            if (vehicle == null)
                continue;
            vehicle.isPlayer = false;
            vehicle.SetSurfaceGrip(TrackData.surfaceGrip);
            Vector3 right = Vector3.Cross(Vector3.up, TrackBuilder.StartTangent).normalized;
            int row = i / 2;
            int column = i % 2;
            vehicle.transform.position = TrackBuilder.StartPoint - TrackBuilder.StartTangent * (12f + row * 7f) + right * (column == 0 ? -2.6f : 2.6f) + Vector3.up * 0.75f;
            vehicle.transform.rotation = Quaternion.LookRotation(TrackBuilder.StartTangent, Vector3.up);
            PrepareLocalGrid(vehicle);
            vehicle.TryStartEngine();
            AIDriver1912 ai = vehicle.gameObject.AddComponent<AIDriver1912>();
            ai.vehicle = vehicle;
            ai.track = TrackBuilder;
            ai.race = this;
            ai.laneOffset = column == 0 ? -2.5f : 2.5f;
            ai.skill = 0.62f + i * 0.06f;
            AI.Add(ai);
        }
    }

    private void PrepareLocalGrid(VehicleController1912 vehicle)
    {
        Rigidbody body = vehicle.GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.position = vehicle.transform.position;
        body.rotation = vehicle.transform.rotation;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        vehicle.SetInput(default);
        vehicle.SetSimulationEnabled(false);
        VehicleInput1912 vehicleInput = vehicle.GetComponent<VehicleInput1912>();
        if (vehicleInput != null) vehicleInput.enabled = false;
        foreach (Collider collider in vehicle.GetComponentsInChildren<Collider>())
            collider.enabled = true;
    }

    private IEnumerator SetupNetworkRace()
    {
        // Do not start a countdown while any peer's car is still spawning or loading.
        while (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            networkCars.Clear();
            networkCars.AddRange(UnityEngine.Object.FindObjectsByType<NetworkPlayerCar>());
            int expected = NetworkManager.Singleton.ConnectedClientsIds.Count;
            bool allReady = expected > 0 && networkCars.Count >= expected;
            foreach (NetworkPlayerCar car in networkCars)
                if (!car.IsSpawned || car.vehicle == null) allReady = false;
            if (allReady)
                break;
            yield return null;
        }
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            yield break;

        networkCars.Sort((a, b) => a.OwnerClientId.CompareTo(b.OwnerClientId));
        if (NetworkManager.Singleton.IsServer)
        {
            for (int i = 0; i < networkCars.Count; i++)
            {
                if (networkCars[i].vehicle != null)
                    networkCars[i].vehicle.SetSurfaceGrip(TrackData.surfaceGrip);
                networkCars[i].PlaceOnGrid(i);
            }
        }
        Player = networkCars.Find(car => car.IsOwner)?.vehicle;
        BeginAfterSetup();
    }

    private void BeginAfterSetup()
    {
        // Checkpoint 0 contains the grid; the next required trigger is checkpoint 1.
        CheckpointIndex = 1;
        CompletedLaps = 0;
        playerContinuousProgress = 0f;
        lastPlayerTrackProgress = -1f;
        SetupCamera();
        if (!networkMode || NetworkManager.Singleton.IsClient)
            HUD = RaceHUD.Create(this);
        State = RaceState.Intro;
        if (networkMode)
            BeginNetworkCountdown();
        else if (GameManager.Instance.SkipRaceStory)
        {
            GameManager.Instance.SkipRaceStory = false;
            BeginCountdown();
        }
        else
        {
            StoryUI.Show(TrackData.storyTitle, TrackData.storyText, TrackData.previewResource, BeginCountdown);
        }
    }

    private void SetupCamera()
    {
        if (Player == null) return;
        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camera = cameraObject.GetComponent<Camera>();
            camera.tag = "MainCamera";
        }
        CameraController1912 controller = camera.GetComponent<CameraController1912>() ?? camera.gameObject.AddComponent<CameraController1912>();
        controller.vehicle = Player;
        controller.targetCamera = camera;
        controller.SetCamera((CameraMode)GameManager.Instance.SelectedCameraMode, false);
        VehicleInput1912 playerInput = Player.GetComponent<VehicleInput1912>();
        if (playerInput != null)
        {
            playerInput.cameraController = controller;
            playerInput.enabled = false;
        }
    }

    private void BeginCountdown()
    {
        StartCoroutine(Countdown());
    }

    private void BeginNetworkCountdown()
    {
        State = RaceState.Countdown;
        HUD?.SetCountdown("Ожидание игроков");
        if (LobbyManager.Instance == null)
        {
            Debug.LogError("[Race] Сетевая гонка без LobbyManager; синхронный старт невозможен.");
            return;
        }
        // Every peer acknowledges after its track and all local car objects are ready.
        if (NetworkManager.Singleton.IsServer)
            LobbyManager.Instance.ServerMarkRaceReady(0);
        else
            LobbyManager.Instance.MarkRaceReadyServerRpc();
    }

    private void UpdateNetworkCountdown()
    {
        if (LobbyManager.Instance == null || NetworkManager.Singleton == null)
            return;
        double startTime = LobbyManager.Instance.RaceStartServerTime.Value;
        if (startTime <= 0d)
        {
            HUD?.SetCountdown("Ожидание игроков");
            return;
        }
        double remaining = startTime - NetworkManager.Singleton.ServerTime.Time;
        if (remaining > 0d)
        {
            int count = Mathf.Clamp(Mathf.CeilToInt((float)remaining), 1, 3);
            HUD?.SetCountdown(count.ToString());
            return;
        }
        BeginNetworkRacing(startTime);
    }

    private void BeginNetworkRacing(double sharedStartTime)
    {
        State = RaceState.Racing;
        Elapsed = Mathf.Max(0f, (float)(NetworkManager.Singleton.ServerTime.Time - sharedStartTime));
        ReleaseGrid();
        EnablePlayerInput();
        HUD?.SetCountdown("СТАРТ!");
        Invoke(nameof(ClearNetworkCountdown), 0.8f);
        HUD?.SetMessage("Синхронный старт · F — ручной старт", 4f);
    }

    private void ClearNetworkCountdown()
    {
        HUD?.SetCountdown("");
    }

    private IEnumerator Countdown()
    {
        State = RaceState.Countdown;
        HUD?.SetCountdown("3");
        yield return new WaitForSeconds(1f);
        HUD?.SetCountdown("2");
        yield return new WaitForSeconds(1f);
        HUD?.SetCountdown("1");
        yield return new WaitForSeconds(1f);
        HUD?.SetCountdown("СТАРТ!");
        State = RaceState.Racing;
        Elapsed = 0f;
        ReleaseGrid();
        EnablePlayerInput();
        HUD?.SetMessage("F — ручной старт · 1 — первая передача", 4f);
        yield return new WaitForSeconds(0.8f);
        HUD?.SetCountdown("");
    }

    private void PruneNetworkCars()
    {
        networkCars.RemoveAll(car => car == null || !car.IsSpawned);
    }

    private void ReleaseGrid()
    {
        if (networkMode)
        {
            PruneNetworkCars();
            if (NetworkManager.Singleton.IsServer)
                foreach (NetworkPlayerCar car in networkCars)
                    car.SetGridPhase(false);
            return;
        }
        if (Player != null)
        {
            Rigidbody body = Player.GetComponent<Rigidbody>();
            body.isKinematic = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.WakeUp();
            Player.SetSimulationEnabled(true);
        }
        foreach (AIDriver1912 ai in AI)
        {
            if (ai.vehicle == null) continue;
            Rigidbody body = ai.vehicle.GetComponent<Rigidbody>();
            body.isKinematic = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.WakeUp();
            ai.vehicle.SetSimulationEnabled(true);
        }
    }

    private void EnablePlayerInput()
    {
        if (FindAnyObjectByType<PauseMenuUI>() != null)
            return;
        VehicleInput1912 playerInput = Player != null ? Player.GetComponent<VehicleInput1912>() : null;
        if (playerInput != null) playerInput.enabled = true;
    }

    private void Update()
    {
        if (Player != null && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && Time.timeScale > 0f && State != RaceState.Finished)
        {
            Pause();
            return;
        }
        if (networkMode && State == RaceState.Countdown)
        {
            UpdateNetworkCountdown();
            return;
        }
        if (State != RaceState.Racing)
            return;
        if (networkMode)
        {
            PruneNetworkCars();
            if (LobbyManager.Instance == null || NetworkManager.Singleton == null)
                return;
            Elapsed = Mathf.Max(0f, (float)(NetworkManager.Singleton.ServerTime.Time - LobbyManager.Instance.RaceStartServerTime.Value));
            NetworkPlayerCar localCar = networkCars.Find(car => car.IsOwner);
            if (localCar != null)
            {
                CheckpointIndex = localCar.CheckpointIndex.Value;
                CompletedLaps = localCar.CompletedLaps.Value;
                playerContinuousProgress = localCar.RaceProgress.Value;
                if (localCar.Finished.Value && !PlayerFinished)
                {
                    UpdatePosition();
                    FinishRace(true);
                    return;
                }
            }
        }
        else if (Player != null)
        {
            Elapsed += Time.deltaTime;
            UpdatePlayerContinuousProgress();
        }
        UpdatePosition();
        UpdateRoadbook();
        if (Player != null && Elapsed >= TrackData.timeLimit && !PlayerFinished)
            FinishRace(false);
    }

    private void UpdatePosition()
    {
        float playerProgress = playerContinuousProgress;
        if (networkMode)
        {
            PruneNetworkCars();
            NetworkPlayerCar localCar = networkCars.Find(car => car.IsOwner);
            if (localCar != null)
                playerProgress = localCar.RaceProgress.Value;
        }
        int position = 1;
        if (networkMode)
        {
            foreach (NetworkPlayerCar car in networkCars)
                if (!car.IsOwner && car.RaceProgress.Value > playerProgress + 0.0001f)
                    position++;
        }
        else
        {
            foreach (AIDriver1912 ai in AI)
                if (ai != null && ai.RaceProgress > playerProgress + 0.0001f)
                    position++;
        }
        CurrentPosition = position;
    }

    private void UpdatePlayerContinuousProgress()
    {
        if (Player == null || TrackBuilder == null || TrackBuilder.Samples.Count == 0)
            return;
        float nearest = NearestTrackProgress(Player.transform.position);
        if (lastPlayerTrackProgress < 0f)
        {
            lastPlayerTrackProgress = nearest;
            return;
        }
        float delta = Mathf.Repeat(nearest - lastPlayerTrackProgress + 0.5f, 1f) - 0.5f;
        if (Mathf.Abs(delta) < 0.1f)
            playerContinuousProgress += delta;
        lastPlayerTrackProgress = nearest;
    }

    private static float NearestTrackProgress(Vector3 position)
    {
        TrackBuilder track = Instance != null ? Instance.TrackBuilder : null;
        if (track == null || track.Samples.Count == 0)
            return 0f;
        float bestDistance = float.PositiveInfinity;
        float bestProgress = 0f;
        for (int i = 0; i < track.Samples.Count; i++)
        {
            Vector3 a = track.Samples[i];
            Vector3 b = track.Samples[(i + 1) % track.Samples.Count];
            Vector3 segment = b - a;
            float lengthSquared = segment.sqrMagnitude;
            float t = lengthSquared > 0.0001f ? Mathf.Clamp01(Vector3.Dot(position - a, segment) / lengthSquared) : 0f;
            float candidateDistance = Vector3.SqrMagnitude(position - (a + segment * t));
            if (candidateDistance < bestDistance)
            {
                bestDistance = candidateDistance;
                bestProgress = (i + t) / track.Samples.Count;
            }
        }
        return bestProgress;
    }

    private void UpdateRoadbook()
    {
        if (TrackBuilder == null) return;
        float progress = Mathf.Repeat(playerContinuousProgress, 1f);
        Vector3 current = TrackBuilder.TangentAt(progress);
        Vector3 next = TrackBuilder.TangentAt(progress + 0.035f);
        float angle = Vector3.SignedAngle(current, next, Vector3.up);
        NextInstruction = angle > 18f ? "Поворот направо" : angle < -18f ? "Поворот налево" : "Прямо";
    }

    public void PlayerCheckpoint(int index)
    {
        // Network progress is calculated and replicated by the server for every car.
        if (networkMode)
            return;
        int expectedIndex = CheckpointIndex >= TrackBuilder.checkpointCount ? 0 : CheckpointIndex;
        if (State != RaceState.Racing || index != expectedIndex)
            return;
        if (index == 0)
        {
            // Checkpoint 0 is valid only after all intermediate checkpoints (1..N-1).
            if (CheckpointIndex != TrackBuilder.checkpointCount)
                return;
            CompletedLaps++;
            CheckpointIndex = 1;
            int raceLaps = GameManager.Instance.GetRaceLaps();
            if (CompletedLaps >= raceLaps)
            {
                playerContinuousProgress = CompletedLaps;
                UpdatePosition();
                FinishRace(true);
            }
            else
            {
                HUD?.SetMessage($"Круг {CompletedLaps + 1}/{raceLaps}", 2.5f);
            }
            return;
        }
        CheckpointIndex = index + 1;
        HUD?.SetMessage($"Контроль {CheckpointIndex}/{TrackBuilder.checkpointCount}", 1.2f);
    }

    public void PlayerRepair()
    {
        if (Player == null || GameManager.Instance.Profile.credits < 50f)
        {
            HUD?.SetMessage("Недостаточно средств или ремонт уже не нужен", 2f);
            return;
        }
        GameManager.Instance.Profile.credits -= 50f;
        GameManager.Instance.Profile.repairs++;
        if (networkMode)
        {
            NetworkPlayerCar localCar = networkCars.Find(car => car.IsOwner);
            if (localCar != null)
                localCar.RequestRepairServerRpc();
        }
        else
        {
            Player.Repair(45f);
        }
        GameManager.Instance.Save();
        HUD?.SetMessage("Машина отремонтирована", 2f);
    }

    public void Pause()
    {
        if (State == RaceState.Finished)
            return;
        PauseMenuUI.Show(this);
    }

    private void FinishRace(bool finished)
    {
        if (State == RaceState.Finished || Player == null)
            return;
        PlayerFinished = finished;
        PlayerWon = finished && CurrentPosition == 1;
        State = RaceState.Finished;
        Reward = finished ? TrackData.reward + Mathf.Max(0, 6 - CurrentPosition) * 75 : 0;
        GameManager.Instance.Profile.credits += Reward;
        GameManager.Instance.Profile.racesFinished++;
        if (PlayerWon)
        {
            GameManager.Instance.Profile.racesWon++;
            AchievementSystem.Instance.Unlock("winner");
        }
        if (finished)
            AchievementSystem.Instance.Unlock("first_finish");
        BestTimeRecord best = GameManager.Instance.Profile.bestTimes.Find(x => x.trackId == TrackData.id);
        if (finished && (best == null || Elapsed < best.seconds))
        {
            if (best == null)
            {
                best = new BestTimeRecord { trackId = TrackData.id };
                GameManager.Instance.Profile.bestTimes.Add(best);
            }
            best.seconds = Elapsed;
        }
        if (finished && TrackData.id == "alps_pass")
            AchievementSystem.Instance.Unlock("alps");
        if (finished && TrackData.id == "monaco_finish")
            AchievementSystem.Instance.Unlock("monaco");
        if (finished)
            GameManager.Instance.RegisterTrackCompletion(TrackData.id);
        else
            GameManager.Instance.Save();
        Player.SetInput(new VehicleController1912.VehicleInput());
        VehicleInput1912 playerInput = Player.GetComponent<VehicleInput1912>();
        if (playerInput != null) playerInput.enabled = false;
        StartCoroutine(ShowResults());
    }

    private IEnumerator ShowResults()
    {
        yield return new WaitForSeconds(1.5f);
        ResultsUI.Show(this);
    }

    public void ReturnToMenu()
    {
        Time.timeScale = 1f;
        if (LanSessionController.Instance != null && LanSessionController.Instance.Network != null && LanSessionController.Instance.Network.IsListening)
            LanSessionController.Instance.ShutdownSession();
        else
            SceneManager.LoadScene("MainMenu");
    }
}
