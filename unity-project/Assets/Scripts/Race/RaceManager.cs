using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class RaceManager : MonoBehaviour
{
    public enum RaceState { Intro, Countdown, Racing, Finished }

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
    public int RacersCount => AI.Count + 1;
    public int Reward { get; private set; }
    public bool PlayerFinished { get; private set; }
    public bool PlayerWon { get; private set; }
    public string NextInstruction { get; private set; } = "Прямо";

    private void Awake()
    {
        EnsureServices();
        TrackData = Resources.Load<TrackData>($"Tracks/{GameManager.Instance.SelectedTrackId}");
        if (TrackData == null)
            TrackData = Resources.Load<TrackData>("Tracks/winter_departure");
        GameObject trackRoot = new GameObject("Track");
        TrackBuilder = trackRoot.AddComponent<TrackBuilder>();
        TrackBuilder.Build(TrackData);
        foreach (Checkpoint checkpoint in TrackBuilder.GetComponentsInChildren<Checkpoint>())
            checkpoint.raceManager = this;
        GameManager.Instance.Profile.racesStarted++;
        SpawnRacers();
        CheckpointIndex = 1;
        SetupCamera();
        HUD = RaceHUD.Create(this);
        State = RaceState.Intro;
        StoryUI.Show(TrackData.storyTitle, TrackData.storyText, TrackData.previewResource, BeginCountdown);
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
        Player = Instantiate(playerPrefab).GetComponent<VehicleController1912>();
        Player.isPlayer = true;
        Player.transform.position = TrackBuilder.StartPoint - TrackBuilder.StartTangent * 2.5f + Vector3.up * 0.8f;
        Player.transform.rotation = Quaternion.LookRotation(TrackBuilder.StartTangent, Vector3.up);
        Player.GetComponent<Rigidbody>().position = Player.transform.position;
        Player.GetComponent<Rigidbody>().rotation = Player.transform.rotation;
        Player.SetSurfaceGrip(TrackData.surfaceGrip);

        string[] rivals = { "benz_1912_racer_concept", "ford_model_t_1912", "fiat_s76_1910", "mercedes_37_90_1912" };
        for (int i = 0; i < TrackData.aiCount; i++)
        {
            GameObject prefab = Resources.Load<GameObject>($"Vehicles/{rivals[i % rivals.Length]}");
            if (prefab == null)
                continue;
            VehicleController1912 vehicle = Instantiate(prefab).GetComponent<VehicleController1912>();
            vehicle.isPlayer = false;
            vehicle.SetSurfaceGrip(TrackData.surfaceGrip);
            Vector3 right = Vector3.Cross(Vector3.up, TrackBuilder.StartTangent).normalized;
            vehicle.transform.position = TrackBuilder.StartPoint - TrackBuilder.StartTangent * (3f + i * 3f) + right * ((i % 2 == 0) ? -1.2f : 1.2f) + Vector3.up * 0.8f;
            vehicle.transform.rotation = Quaternion.LookRotation(TrackBuilder.StartTangent, Vector3.up);
            vehicle.GetComponent<Rigidbody>().position = vehicle.transform.position;
            vehicle.GetComponent<Rigidbody>().rotation = vehicle.transform.rotation;
            vehicle.TryStartEngine();
            AIDriver1912 ai = vehicle.gameObject.AddComponent<AIDriver1912>();
            ai.vehicle = vehicle;
            ai.track = TrackBuilder;
            ai.skill = 0.62f + i * 0.06f;
            ai.progress = -0.008f * (i + 1);
            AI.Add(ai);
        }
    }

    private void SetupCamera()
    {
        Camera camera = Camera.main;
        if (camera == null)
        {
            GameObject cameraObject = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camera = cameraObject.GetComponent<Camera>();
            camera.tag = "MainCamera";
        }
        CameraController1912 controller = camera.gameObject.AddComponent<CameraController1912>();
        controller.vehicle = Player;
        controller.targetCamera = camera;
        controller.mode = 0;
        VehicleInput1912 playerInput = Player.GetComponent<VehicleInput1912>();
        if (playerInput != null) playerInput.cameraController = controller;
    }

    private void BeginCountdown()
    {
        StartCoroutine(Countdown());
    }

    private IEnumerator Countdown()
    {
        State = RaceState.Countdown;
        HUD.SetCountdown("3");
        yield return new WaitForSeconds(1f);
        HUD.SetCountdown("2");
        yield return new WaitForSeconds(1f);
        HUD.SetCountdown("1");
        yield return new WaitForSeconds(1f);
        HUD.SetCountdown("СТАРТ!");
        yield return new WaitForSeconds(0.8f);
        HUD.SetCountdown("");
        State = RaceState.Racing;
        Elapsed = 0f;
        HUD.SetMessage("F — ручной старт · 1 — первая передача", 4f);
    }

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame && Time.timeScale > 0f && State != RaceState.Finished)
        {
            Pause();
            return;
        }
        if (State != RaceState.Racing)
            return;
        Elapsed += Time.deltaTime;
        UpdatePosition();
        UpdateRoadbook();
        if (Elapsed >= TrackData.timeLimit && !PlayerFinished)
            FinishRace(false);
    }

    private void UpdatePosition()
    {
        float playerProgress = CompletedLaps + CheckpointIndex / (float)TrackBuilder.checkpointCount;
        int position = 1;
        foreach (AIDriver1912 ai in AI)
            if (ai.RaceProgress > playerProgress)
                position++;
        CurrentPosition = position;
    }

    private void UpdateRoadbook()
    {
        float progress = (CompletedLaps + CheckpointIndex / (float)TrackBuilder.checkpointCount) % 1f;
        Vector3 current = TrackBuilder.TangentAt(progress);
        Vector3 next = TrackBuilder.TangentAt(progress + 0.035f);
        float angle = Vector3.SignedAngle(current, next, Vector3.up);
        NextInstruction = angle > 18f ? "Поворот направо" : angle < -18f ? "Поворот налево" : "Прямо";
    }

    public void PlayerCheckpoint(int index)
    {
        if (State != RaceState.Racing || index != CheckpointIndex)
            return;
        CheckpointIndex++;
        HUD.SetMessage($"Контроль {CheckpointIndex}/{TrackBuilder.checkpointCount}", 1.2f);
        if (CheckpointIndex >= TrackBuilder.checkpointCount)
        {
            CheckpointIndex = 0;
            CompletedLaps++;
            if (CompletedLaps >= TrackData.laps)
                FinishRace(true);
            else
                HUD.SetMessage($"Круг {CompletedLaps + 1}/{TrackData.laps}", 2.5f);
        }
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
        Player.Repair(45f);
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
        if (State == RaceState.Finished)
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
        if (finished && TrackData.id == "winter_departure" && !GameManager.Instance.Profile.unlockedTracks.Contains("alps_pass"))
            GameManager.Instance.Profile.unlockedTracks.Add("alps_pass");
        if (finished && TrackData.id == "alps_pass")
            AchievementSystem.Instance.Unlock("alps");
        if (finished && TrackData.id == "monaco_finish")
        {
            GameManager.Instance.Profile.unlockedTracks.Add("monaco_finish");
            AchievementSystem.Instance.Unlock("monaco");
        }
        GameManager.Instance.Save();
        Player.SetInput(new VehicleController1912.VehicleInput());
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
        SceneManager.LoadScene("MainMenu");
    }
}
