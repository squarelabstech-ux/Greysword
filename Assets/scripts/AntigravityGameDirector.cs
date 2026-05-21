using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

/// <summary>
/// AntigravityGameDirector — Master orchestrator of the agentic AI system.
/// Runs every `evaluationInterval` seconds. Observes → Infers → Decides → Acts → Evaluates.
///
/// THIS IS THE HACKATHON DEMO CENTERPIECE.
/// It represents Antigravity as the "brain of the game" that adapts gameplay in real-time.
/// Can communicate with a remote server over HTTP (for mobile builds) or fallback to local calculations.
///
/// SETUP:
/// - Attach to a GameDirector GameObject with:
///   PlayerBehaviorTracker, AdaptiveDifficultyManager, AgenticDecisionLogger,
///   ZombieSpawnManager, GameModeManager
/// </summary>
public class AntigravityGameDirector : MonoBehaviour
{
    [Header("Evaluation")]
    [Tooltip("How often (in seconds) Antigravity evaluates player state and adapts")]
    public float evaluationInterval = 5f;

    [Header("Initial Difficulty")]
    [Tooltip("Starting difficulty profile — will be adapted at runtime")]
    public DifficultyProfile currentProfile;

    [Header("Remote AI Brain Connection")]
    [Tooltip("URL of the external Antigravity Brain Server (e.g. http://192.168.1.5:8080)")]
    public string serverUrl = "http://127.0.0.1:8080";

    [Header("References (auto-found if empty)")]
    public PlayerBehaviorTracker    behaviorTracker;
    public AdaptiveDifficultyManager difficultyManager;
    public AgenticDecisionLogger    logger;
    public ZombieSpawnManager       spawnManager;
    public GameModeManager          gameModeManager;

    // ─── Singleton ─────────────────────────────────────────────────────────────
    public static AntigravityGameDirector Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        // Initialize with Normal difficulty
        if (currentProfile == null)
            currentProfile = DifficultyProfile.Normal();

        // Load saved server URL from PlayerPrefs for persistence across plays/devices
        serverUrl = PlayerPrefs.GetString("AntigravityServerUrl", "http://127.0.0.1:8080");
    }

    // ─── Private State ─────────────────────────────────────────────────────────

    private float evalTimer     = 0f;
    private bool  playerIsDead  = false;
    private int   evalCycleCount = 0;

    // Metrics for outcome tracking
    private float healthAtLastEval = 1f;
    private int   killsAtLastEval  = 0;

    // ─── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        AutoFindReferences();
        PlayerHealth.OnPlayerDeath += OnPlayerDeath;

        Debug.Log("[AntigravityGameDirector] Antigravity brain activated. Evaluation every " + evaluationInterval + "s.");
        Debug.Log($"[AntigravityGameDirector] Starting with profile: {currentProfile}");
        Debug.Log($"[AntigravityGameDirector] Remote server configured at: {serverUrl}");

        StartCoroutine(SendSessionStart());

        // Apply initial difficulty immediately
        ApplyProfileToGame(currentProfile);
    }

    void OnDestroy()
    {
        PlayerHealth.OnPlayerDeath -= OnPlayerDeath;
    }

    void OnPlayerDeath()
    {
        if (playerIsDead) return;
        playerIsDead = true;
        StartCoroutine(SendSessionEnd("Player Died"));
    }

    void OnApplicationQuit()
    {
        SendSessionEndSync("Game Exited / Application Closed");
    }

    private IEnumerator SendSessionStart()
    {
        string url = serverUrl + "/session_start";
        UnityWebRequest webRequest = new UnityWebRequest(url, "POST");
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.timeout = 2;
        yield return webRequest.SendWebRequest();
    }

    private IEnumerator SendSessionEnd(string reason)
    {
        string url = serverUrl + "/session_end";
        string jsonPayload = $"{{\"reason\":\"{reason}\"}}";
        byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(jsonPayload);

        UnityWebRequest webRequest = new UnityWebRequest(url, "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(jsonBytes);
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.timeout = 2;
        yield return webRequest.SendWebRequest();
    }

    private void SendSessionEndSync(string reason)
    {
        try
        {
            string url = serverUrl + "/session_end";
            string jsonPayload = $"{{\"reason\":\"{reason}\"}}";
            byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(jsonPayload);

            UnityWebRequest webRequest = new UnityWebRequest(url, "POST");
            webRequest.uploadHandler = new UploadHandlerRaw(jsonBytes);
            webRequest.downloadHandler = new DownloadHandlerBuffer();
            webRequest.SetRequestHeader("Content-Type", "application/json");
            webRequest.timeout = 1;
            webRequest.SendWebRequest();
        }
        catch {}
    }

    void Update()
    {
        if (playerIsDead) return;
        if (!GameUIManager.IsGamePlaying || GameUIManager.IsGamePaused) return;

        // Skip evaluation in FixedRule mode — GameModeManager handles it
        if (gameModeManager != null && gameModeManager.CurrentMode == GameMode.FixedRule) return;

        // Explicitly command zombie spawning
        if (spawnManager != null)
        {
            spawnManager.AntigravitySpawnUpdate(Time.deltaTime);
        }

        evalTimer += Time.deltaTime;
        if (evalTimer >= evaluationInterval)
        {
            evalTimer = 0f;
            RunEvaluationCycle();
        }
    }

    // ─── Evaluation Cycle ─────────────────────────────────────────────────────

    void RunEvaluationCycle()
    {
        evalCycleCount++;
        Debug.Log($"[AntigravityGameDirector] === EVALUATION CYCLE #{evalCycleCount} ===");

        if (behaviorTracker == null || difficultyManager == null || logger == null) return;

        // ── 1. Record outcome of PREVIOUS decision ─────────────────────────────
        if (evalCycleCount > 1)
        {
            PlayerHealth ph = FindObjectOfType<PlayerHealth>();
            float currentHealth = ph != null ? ph.HealthFraction : 0f;
            int   currentKills  = behaviorTracker.ZombiesKilled;

            int   killsSinceLast   = currentKills - killsAtLastEval;
            float healthChange     = currentHealth - healthAtLastEval;
            string outcome = $"Health {(healthChange >= 0 ? "+" : "")}{healthChange*100:F0}%, " +
                             $"{killsSinceLast} kills since last eval, " +
                             $"current HP={currentHealth*100:F0}%";
            logger.RecordOutcome(outcome);

            // Send outcome update to remote server console
            StartCoroutine(SendLogToRemote(outcome));
        }

        // ── 2. Observe ─────────────────────────────────────────────────────────
        BehaviorSnapshot snapshot = behaviorTracker.GetSnapshot();

        // ── 3. Evaluate (Remote Server Evaluation with Local Fallback) ──────────
        StartCoroutine(EvaluateRemoteCoroutine(snapshot, currentProfile));
    }

    private IEnumerator EvaluateRemoteCoroutine(BehaviorSnapshot snapshot, DifficultyProfile current)
    {
        string url = serverUrl + "/evaluate";
        AntigravityRequest requestData = new AntigravityRequest
        {
            snapshot = snapshot,
            currentProfile = current
        };
        
        string jsonPayload = JsonUtility.ToJson(requestData);
        byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(jsonPayload);

        UnityWebRequest webRequest = new UnityWebRequest(url, "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(jsonBytes);
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.timeout = 2; // 2 seconds timeout to keep gameplay responsive

        yield return webRequest.SendWebRequest();

        if (webRequest.result == UnityWebRequest.Result.Success)
        {
            string responseJson = webRequest.downloadHandler.text;
            try
            {
                AntigravityResponse response = JsonUtility.FromJson<AntigravityResponse>(responseJson);
                if (response != null && response.profile != null)
                {
                    Debug.Log($"[AntigravityGameDirector] Remote decision received: {response.label} -> {response.decision}");
                    ApplyEvaluationResult(response.profile, response.label, response.observation, response.decision, response.action, response.rejected, response.rejectReason);
                    yield break;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[AntigravityGameDirector] Error parsing remote response: {ex.Message}");
            }
        }
        else
        {
            Debug.LogWarning($"[AntigravityGameDirector] Remote server evaluation failed ({webRequest.error}). Falling back to local AI brain.");
        }

        // FALLBACK: Execute on-device AdaptiveDifficultyManager local logic
        var localResult = difficultyManager.Evaluate(snapshot, current);
        ApplyEvaluationResult(localResult.profile, localResult.label, localResult.observation, localResult.decision, localResult.action, localResult.rejected, localResult.rejectReason);
    }

    private IEnumerator SendLogToRemote(string logMessage)
    {
        string url = serverUrl + "/log";
        string jsonPayload = $"{{\"log\":\"{logMessage}\"}}";
        byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(jsonPayload);

        UnityWebRequest webRequest = new UnityWebRequest(url, "POST");
        webRequest.uploadHandler = new UploadHandlerRaw(jsonBytes);
        webRequest.downloadHandler = new DownloadHandlerBuffer();
        webRequest.SetRequestHeader("Content-Type", "application/json");
        webRequest.timeout = 2;

        yield return webRequest.SendWebRequest();
    }

    void ApplyEvaluationResult(DifficultyProfile newProfile, string label, string observation, string decision, string action, bool rejected, string rejectReason)
    {
        // ── 4. Log ────────────────────────────────────────────────────────────
        logger.LogDecision(observation, label, decision, action, rejected, rejectReason);

        // ── 5. Act — apply changes to game systems ─────────────────────────────
        currentProfile = newProfile;
        ApplyProfileToGame(currentProfile);

        // Store for outcome comparison next cycle
        PlayerHealth p = FindObjectOfType<PlayerHealth>();
        healthAtLastEval = p != null ? p.HealthFraction : 0f;
        killsAtLastEval  = behaviorTracker.ZombiesKilled;

        // Also record health sample for post-fight average
        if (p != null)
            behaviorTracker.RecordPostFightHealth(p.HealthFraction);

        // Update GameModeManager metrics
        if (gameModeManager != null)
            gameModeManager.RecordEvaluation(label, rejected);
            
        // Antigravity dynamic behavior: Ambush camping players
        if (label == "Kiter" || behaviorTracker.GetSnapshot().isKiting || behaviorTracker.GetSnapshot().avgDistFromZombies > 18f)
        {
            if (spawnManager != null)
            {
                Debug.Log("[AntigravityBrain] Player is camping/kiting. Spawning ambush!");
                spawnManager.SpawnAmbush(3);
                logger.LogDecision("Player camping detected", "Needs immediate pressure", "Spawn ambush", "Spawned 3 zombies behind player");
            }
        }

        Debug.Log($"[AntigravityGameDirector] New profile applied: {currentProfile}");
    }

    // ─── Apply Profile ─────────────────────────────────────────────────────────

    public void ApplyProfileToGame(DifficultyProfile profile)
    {
        // Apply to spawn manager
        if (spawnManager != null)
        {
            spawnManager.ApplyDifficultySettings(profile.maxAliveZombies, profile.spawnInterval);
            spawnManager.ApplyDifficultyToActiveZombies(
                profile.zombieSpeedMultiplier,
                profile.zombieDamageMultiplier,
                profile.zombieHealthMultiplier
            );
        }

        // Apply detection/chase ranges to all active ZombieAIs
        ZombieAI[] zombies = FindObjectsOfType<ZombieAI>();
        foreach (ZombieAI z in zombies)
        {
            if (!z.gameObject.activeSelf) continue;
            z.detectionRange = profile.zombieDetectionRange;
            z.chaseRange     = profile.zombieChaseRange;
            z.attackCooldown = profile.zombieAttackCooldown;
        }
    }

    // ─── Helpers ───────────────────────────────────────────────────────────────

    void AutoFindReferences()
    {
        if (behaviorTracker  == null) behaviorTracker  = GetComponent<PlayerBehaviorTracker>()    ?? FindObjectOfType<PlayerBehaviorTracker>();
        if (difficultyManager == null) difficultyManager = GetComponent<AdaptiveDifficultyManager>() ?? FindObjectOfType<AdaptiveDifficultyManager>();
        if (logger            == null) logger            = GetComponent<AgenticDecisionLogger>()       ?? FindObjectOfType<AgenticDecisionLogger>();
        if (spawnManager      == null) spawnManager      = FindObjectOfType<ZombieSpawnManager>();
        if (gameModeManager   == null) gameModeManager   = FindObjectOfType<GameModeManager>();
    }

    // ─── Debug / Editor Helpers ────────────────────────────────────────────────

    [ContextMenu("Force Evaluation Now")]
    public void ForceEvaluationNow()
    {
        evalTimer = evaluationInterval; // will trigger on next Update
        Debug.Log("[AntigravityGameDirector] Force evaluation triggered.");
    }

    public DifficultyProfile CurrentProfile => currentProfile;
    public int EvalCycleCount => evalCycleCount;
}

// ─── JSON Helper Structs for Network Serialization ───────────────────────────

[System.Serializable]
public class AntigravityRequest
{
    public BehaviorSnapshot snapshot;
    public DifficultyProfile currentProfile;
}

[System.Serializable]
public class AntigravityResponse
{
    public DifficultyProfile profile;
    public string label;
    public string observation;
    public string decision;
    public string action;
    public bool rejected;
    public string rejectReason;
}
