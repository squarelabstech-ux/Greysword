using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// GameModeManager — Toggles between FixedRule and Agentic modes.
/// Tracks comparison metrics for hackathon evaluation.
///
/// SETUP:
/// - Attach to GameDirector GameObject.
/// - Set initialMode to Agentic for hackathon demo (or FixedRule for control comparison).
/// - Optionally assign UI Text fields for live metrics display.
/// </summary>
public enum GameMode { FixedRule, Agentic }

public class GameModeManager : MonoBehaviour
{
    [Header("Mode")]
    public GameMode initialMode = GameMode.Agentic;

    [Header("Fixed Rule Waves (only used in FixedRule mode)")]
    [Tooltip("Seconds from start before upgrading to Normal difficulty")]
    public float waveNormalAt = 120f;

    [Tooltip("Seconds from start before upgrading to Hard difficulty")]
    public float waveHardAt = 300f;

    [Header("UI (optional)")]
    public Text modeLabelText;
    public Text survivalTimeText;
    public Text zombieKillsText;
    public Text deathCountText;
    public Text avgHealthText;
    public Text evalCountText;

    // ─── Singleton ─────────────────────────────────────────────────────────────
    public static GameModeManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        currentMode = initialMode;
    }

    // ─── Private State ─────────────────────────────────────────────────────────

    private GameMode currentMode;
    private float    sessionTime   = 0f;
    private bool     gameRunning   = true;
    private int      evalCount     = 0;
    private int      rejectCount   = 0;
    private float    uiRefreshTimer = 0f;

    private ZombieSpawnManager spawnManager;

    // ─── Properties ────────────────────────────────────────────────────────────

    public GameMode CurrentMode => currentMode;

    // ─── Lifecycle ─────────────────────────────────────────────────────────────

    void Start()
    {
        spawnManager = FindObjectOfType<ZombieSpawnManager>();
        PlayerHealth.OnPlayerDeath += OnPlayerDied;

        if (modeLabelText != null)
            modeLabelText.text = $"Mode: {currentMode}";

        Debug.Log($"[GameModeManager] Mode = {currentMode}");
    }

    void Update()
    {
        if (!gameRunning) return;

        sessionTime += Time.deltaTime;

        // Fixed rule wave progression
        if (currentMode == GameMode.FixedRule)
        {
            if (sessionTime >= waveHardAt)
                ApplyFixedProfile(DifficultyProfile.Hard());
            else if (sessionTime >= waveNormalAt)
                ApplyFixedProfile(DifficultyProfile.Normal());
        }

        // Refresh UI every second
        uiRefreshTimer -= Time.deltaTime;
        if (uiRefreshTimer <= 0f)
        {
            uiRefreshTimer = 1f;
            RefreshUI();
        }
    }

    void ApplyFixedProfile(DifficultyProfile profile)
    {
        if (spawnManager == null) return;
        spawnManager.ApplyDifficultySettings(profile.maxAliveZombies, profile.spawnInterval);
        spawnManager.ApplyDifficultyToActiveZombies(
            profile.zombieSpeedMultiplier,
            profile.zombieDamageMultiplier,
            profile.zombieHealthMultiplier
        );
    }

    void OnPlayerDied()
    {
        gameRunning = false;
        Debug.Log($"[GameModeManager] Session ended. Mode={currentMode} Time={sessionTime:F0}s");
        PrintFinalMetrics();
    }

    // ─── Metrics ───────────────────────────────────────────────────────────────

    /// <summary>Called by AntigravityGameDirector each evaluation cycle.</summary>
    public void RecordEvaluation(string label, bool rejected)
    {
        evalCount++;
        if (rejected) rejectCount++;
    }

    void RefreshUI()
    {
        PlayerBehaviorTracker tracker = PlayerBehaviorTracker.Instance;
        if (tracker == null) return;

        if (survivalTimeText != null)
            survivalTimeText.text = $"Survived: {sessionTime:F0}s";

        if (zombieKillsText != null)
            zombieKillsText.text = $"Kills: {tracker.ZombiesKilled}";

        if (deathCountText != null)
            deathCountText.text = $"Deaths: {tracker.DeathCount}";

        if (avgHealthText != null)
            avgHealthText.text = $"Avg HP: {tracker.AverageHealthAfterFight * 100:F0}%";

        if (evalCountText != null)
            evalCountText.text = $"AI Evals: {evalCount} (rejected: {rejectCount})";
    }

    void PrintFinalMetrics()
    {
        PlayerBehaviorTracker tracker = PlayerBehaviorTracker.Instance;
        if (tracker == null) return;

        Debug.Log($"=== HACKATHON FINAL METRICS ===\n" +
                  $"Mode:              {currentMode}\n" +
                  $"Survival Time:     {sessionTime:F1}s\n" +
                  $"Zombies Killed:    {tracker.ZombiesKilled}\n" +
                  $"Death Count:       {tracker.DeathCount}\n" +
                  $"Avg Health After Fight: {tracker.AverageHealthAfterFight*100:F0}%\n" +
                  $"AI Evaluations:    {evalCount}\n" +
                  $"QC Rejections:     {rejectCount}");
    }

    [ContextMenu("Switch to FixedRule Mode")]
    public void SwitchToFixedRule()
    {
        currentMode = GameMode.FixedRule;
        if (modeLabelText != null) modeLabelText.text = "Mode: FixedRule";
        Debug.Log("[GameModeManager] Switched to FixedRule mode.");
    }

    [ContextMenu("Switch to Agentic Mode")]
    public void SwitchToAgentic()
    {
        currentMode = GameMode.Agentic;
        if (modeLabelText != null) modeLabelText.text = "Mode: Agentic";
        Debug.Log("[GameModeManager] Switched to Agentic mode.");
    }
}
