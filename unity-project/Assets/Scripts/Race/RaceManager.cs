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
    public float PlayerLaneOffset { get; private set; }
    public int PlayerGridSlot { get; private set; } = -1;

    private struct GridEntry
    {
        public VehicleController1912 vehicle;
        public int slot;
        public bool released;
    }

    private bool networkMode;
    private float playerContinuousProgress;
    private float lastPlayerTrackProgress = -1f;
    private double networkStartTime;
    private int nextNetworkFinishOrder = 1;
    private readonly List<NetworkPlayerCar> networkCars = new List<NetworkPlayerCar>();
    private readonly List<GridEntry> localGrid = new List<GridEntry>();
    private readonly HashSet<NetworkPlayerCar> releasedNetworkCars = new HashSet<NetworkPlayerCar>();

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
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            string sceneTrackId = sceneName switch
            {
                "Race_Alps" => "alps_pass",
                "Race_Monaco" => "monaco_finish",
                "Race_Moscow" => "moscow_highway",
                "Race_Finland" => "finland_coast",
                "Race_Runtime" => GameManager.Instance.SelectedTrackId,
                _ => "winter_departure"
            };
            if (GameManager.LoadTrack(sceneTrackId) != null)
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
        string[] rivals = { "benz_1912_racer_concept", "ford_model_t_1912", "fiat_s76_1910", "mercedes_37_90_1912" };
        int aiCount = GameManager.Instance.GetRaceAiCount();
        if (playerVehicle != null)
        {
            Player = playerVehicle;
            Player.isPlayer = true;
            Player.SetTransmissionMode(GameManager.Instance.Settings.automaticTransmission);
            // Leave the player alone in the protected rear row, using the exact same GridPose as the AI.
            PlayerGridSlot = aiCount + aiCount % 2;
            TrackBuilder.GridPose(PlayerGridSlot, out Vector3 playerPosition, out Quaternion playerRotation);
            Player.transform.SetPositionAndRotation(playerPosition, playerRotation);
            PlayerLaneOffset = LaneOffsetFromGridPose(PlayerGridSlot, playerPosition);
            Player.SetSurfaceGrip(TrackData.surfaceGrip);
            Player.StartEngineImmediately();
            PrepareLocalGrid(Player);
        }

        for (int i = 0; i < aiCount; i++)
        {
            GameObject prefab = Resources.Load<GameObject>($"Vehicles/{rivals[i % rivals.Length]}");
            VehicleController1912 vehicle = prefab != null ? Instantiate(prefab).GetComponent<VehicleController1912>() : null;
            if (vehicle == null)
                continue;
            vehicle.isPlayer = false;
            vehicle.SetSurfaceGrip(TrackData.surfaceGrip);
            TrackBuilder.GridPose(i, out Vector3 position, out Quaternion rotation);
            vehicle.transform.SetPositionAndRotation(position, rotation);
            PrepareLocalGrid(vehicle);
            vehicle.StartEngineImmediately();
            AIDriver1912 ai = vehicle.gameObject.AddComponent<AIDriver1912>();
            ai.vehicle = vehicle;
            ai.track = TrackBuilder;
            ai.race = this;
            ai.gridSlot = i;
            ai.laneOffset = LaneOffsetFromGridPose(i, position);
            ai.skill = Mathf.Clamp(0.68f + i * 0.045f, 0.68f, 0.94f);
            AI.Add(ai);
        }
    }

    private void PrepareLocalGrid(VehicleController1912 vehicle)
    {
        Rigidbody body = vehicle.GetComponent<Rigidbody>();
        body.isKinematic = true;
        body.position = vehicle.transform.position;
        body.rotation = vehicle.transform.rotation;
        vehicle.SetInput(default);
        vehicle.SetSimulationEnabled(false);
        VehicleInput1912 vehicleInput = vehicle.GetComponent<VehicleInput1912>();
        if (vehicleInput != null) vehicleInput.enabled = false;
        foreach (Collider collider in vehicle.GetComponentsInChildren<Collider>())
            collider.enabled = true;
    }

    public static float GridReleaseDelay(int slot)
    {
        // Both columns of a row release together; every peer derives the same delay from GridSlot.
        return Mathf.Max(0, slot) / 2 * 0.7f;
    }

    private float LaneOffsetFromGridPose(int slot, Vector3 position)
    {
        float nearest = NearestTrackProgress(position);
        return Vector3.Dot(position - TrackBuilder.PointAt(nearest), TrackBuilder.RightAt(nearest));
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
        NetworkPlayerCar localCar = networkCars.Find(car => car.IsOwner);
        Player = localCar?.vehicle;
        if (localCar != null)
        {
            PlayerGridSlot = localCar.GridSlot.Value;
            if (PlayerGridSlot >= 0)
                PlayerLaneOffset = LaneOffsetFromGridPose(PlayerGridSlot, Player.transform.position);
        }
        BeginAfterSetup();
    }

    private void BeginAfterSetup()
    {
        // Checkpoint 0 contains the grid; the next required trigger is checkpoint 1.
        CheckpointIndex = 1;
        CompletedLaps = 0;
        InitializePlayerProgress();
        networkStartTime = 0d;
        nextNetworkFinishOrder = 1;
        localGrid.Clear();
        releasedNetworkCars.Clear();
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
        networkStartTime = sharedStartTime;
        State = RaceState.Racing;
        Elapsed = Mathf.Max(0f, (float)(NetworkManager.Singleton.ServerTime.Time - sharedStartTime));
        ReleaseGrid();
        HUD?.SetCountdown("СТАРТ!");
        Invoke(nameof(ClearNetworkCountdown), 0.8f);
        HUD?.SetMessage("Двигатель заведён · W — газ · R — задний ход", 4f);
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
        HUD?.SetMessage("Старт по рядам · W — газ · R — задний ход", 4f);
        yield return new WaitForSeconds(0.8f);
        HUD?.SetCountdown("");
    }

    private void PruneNetworkCars()
    {
        networkCars.RemoveAll(car => car == null || !car.IsSpawned);
    }

    public int RegisterNetworkFinish(NetworkPlayerCar car)
    {
        if (!networkMode || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer || car == null)
            return 0;
        if (car.FinishOrder.Value > 0)
            return car.FinishOrder.Value;
        car.FinishOrder.Value = nextNetworkFinishOrder++;
        return car.FinishOrder.Value;
    }

    private void ReleaseGrid()
    {
        localGrid.Clear();
        releasedNetworkCars.Clear();
        if (networkMode)
            return;
        if (Player != null)
            localGrid.Add(new GridEntry { vehicle = Player, slot = PlayerGridSlot });
        foreach (AIDriver1912 ai in AI)
        {
            if (ai != null && ai.vehicle != null)
                localGrid.Add(new GridEntry { vehicle = ai.vehicle, slot = ai.gridSlot });
        }
    }

    private void UpdateLocalGridRelease()
    {
        for (int i = 0; i < localGrid.Count; i++)
        {
            GridEntry entry = localGrid[i];
            if (entry.released || entry.vehicle == null || Elapsed + 0.0001f < GridReleaseDelay(entry.slot))
                continue;
            ReleaseLocalVehicle(entry.vehicle);
            entry.released = true;
            localGrid[i] = entry;
            if (entry.vehicle == Player)
                EnablePlayerInput();
        }
    }

    private void ReleaseLocalVehicle(VehicleController1912 vehicle)
    {
        Rigidbody body = vehicle.GetComponent<Rigidbody>();
        if (body == null)
            return;
        body.isKinematic = false;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.WakeUp();
        vehicle.SetSimulationEnabled(true);
    }

    private void UpdateNetworkGridRelease()
    {
        if (!NetworkManager.Singleton.IsServer)
            return;
        PruneNetworkCars();
        double now = NetworkManager.Singleton.ServerTime.Time;
        foreach (NetworkPlayerCar car in networkCars)
        {
            if (car == null || releasedNetworkCars.Contains(car) || car.GridSlot.Value < 0)
                continue;
            if (now + 0.0001d < networkStartTime + GridReleaseDelay(car.GridSlot.Value))
                continue;
            car.SetGridPhase(false);
            releasedNetworkCars.Add(car);
        }
    }

    private void UpdateNetworkPlayerInput()
    {
        if (Player == null || NetworkManager.Singleton == null)
            return;
        NetworkPlayerCar localCar = networkCars.Find(car => car.IsOwner);
        if (localCar == null)
            return;
        if (localCar.GridSlot.Value >= 0)
        {
            PlayerGridSlot = localCar.GridSlot.Value;
            PlayerLaneOffset = LaneOffsetFromGridPose(PlayerGridSlot, Player.transform.position);
        }
        if (PlayerGridSlot < 0)
            return;
        double releaseTime = networkStartTime + GridReleaseDelay(PlayerGridSlot);
        if (NetworkManager.Singleton.ServerTime.Time >= releaseTime)
            EnablePlayerInput();
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
            UpdateNetworkGridRelease();
            NetworkPlayerCar localCar = networkCars.Find(car => car.IsOwner);
            if (localCar != null)
            {
                CheckpointIndex = localCar.CheckpointIndex.Value;
                CompletedLaps = localCar.CompletedLaps.Value;
                playerContinuousProgress = localCar.RaceProgress.Value;
                UpdateNetworkPlayerInput();
                if (localCar.FinishOrder.Value > 0 && !PlayerFinished)
                {
                    UpdatePosition();
                    FinishRace(true);
                    return;
                }
            }
        }
        else
        {
            Elapsed += Time.deltaTime;
            UpdateLocalGridRelease();
            if (Player != null)
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
            NetworkPlayerCar localCar = networkCars.Find(car => car.IsOwner);
            bool localFinished = localCar != null && localCar.FinishOrder.Value > 0;
            foreach (NetworkPlayerCar car in networkCars)
            {
                if (car.IsOwner)
                    continue;
                bool carFinished = car.FinishOrder.Value > 0;
                bool ahead;
                if (localFinished || carFinished)
                    ahead = carFinished && (!localFinished || car.FinishOrder.Value < localCar.FinishOrder.Value);
                else
                    ahead = car.RaceProgress.Value > playerProgress + 0.0001f;
                if (ahead)
                    position++;
            }
        }
        else
        {
            foreach (AIDriver1912 ai in AI)
                if (ai != null && ai.RaceProgress > playerProgress + 0.0001f)
                    position++;
        }
        CurrentPosition = position;
    }

    internal static float SignedTrackProgress(float nearest)
    {
        return nearest > 0.75f ? nearest - 1f : nearest;
    }

    private void InitializePlayerProgress()
    {
        if (Player == null || TrackBuilder == null || TrackBuilder.Samples.Count == 0)
        {
            playerContinuousProgress = 0f;
            lastPlayerTrackProgress = -1f;
            return;
        }
        float nearest = NearestTrackProgress(Player.transform.position);
        playerContinuousProgress = SignedTrackProgress(nearest);
        lastPlayerTrackProgress = nearest;
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
        float forwardAlignment = Vector3.Dot(Player.transform.forward, TrackBuilder.TangentAt(nearest));
        if (Mathf.Abs(delta) < 0.035f && forwardAlignment > 0.15f)
            playerContinuousProgress += Mathf.Max(0f, delta);
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

    public void PlayerCheckpoint(int index, VehicleController1912 vehicle = null)
    {
        // Network progress is calculated and replicated by the server for every car.
        if (networkMode)
            return;
        if (vehicle != null)
        {
            float alignment = Vector3.Dot(vehicle.transform.forward, TrackBuilder.TangentAt(index / (float)TrackBuilder.checkpointCount));
            if (alignment < 0.1f)
            {
                HUD?.SetMessage("Неверное направление", 1.2f);
                return;
            }
        }
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
